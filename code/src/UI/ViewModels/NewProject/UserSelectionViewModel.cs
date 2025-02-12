﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI;
using Microsoft.Templates.Core;
using Microsoft.Templates.Core.Diagnostics;
using Microsoft.Templates.Core.Gen;
using Microsoft.Templates.UI.Controls;
using Microsoft.Templates.UI.Mvvm;
using Microsoft.Templates.UI.Resources;
using Microsoft.Templates.UI.Services;
using Microsoft.Templates.UI.ViewModels.Common;

namespace Microsoft.Templates.UI.ViewModels.NewProject
{
    public enum TemplateOrigin
    {
        Layout,
        UserSelection,
    }

    public class UserSelectionViewModel : Observable
    {
        private bool _isInitialized;
        private UserSelectionContext _context;

        public ObservableCollection<LicenseViewModel> Licenses { get; } = new ObservableCollection<LicenseViewModel>();

        public bool HasItemsAddedByUser { get; private set; }

        public ObservableCollection<UserSelectionGroup> Groups { get; } = new ObservableCollection<UserSelectionGroup>();

        public UserSelectionViewModel()
        {
            Groups.Add(new UserSelectionGroup(TemplateType.Page, StringRes.ProjectDetailsPagesSectionTitle, true));
            Groups.Add(new UserSelectionGroup(TemplateType.Feature, StringRes.ProjectDetailsFeaturesSectionTitle));
            Groups.Add(new UserSelectionGroup(TemplateType.Service, StringRes.ProjectDetailsServicesSectionTitle));
            Groups.Add(new UserSelectionGroup(TemplateType.Testing, StringRes.ProjectDetailsTestingSectionTitle));
        }

        public void Initialize(UserSelectionContext context)
        {
            _context = context;

            if (_isInitialized)
            {
                Groups.ToList().ForEach(g => g.Clear());
            }

            _isInitialized = true;
        }

        public async Task AddLayoutTemplatesAsync()
        {
            var layout = GenContext.ToolBox.Repo.GetLayoutTemplates(_context);
            foreach (var item in layout)
            {
                if (item.Template != null)
                {
                    var template = MainViewModel.Instance.GetTemplate(item.Template);
                    if (template != null)
                    {
                        await AddAsync(TemplateOrigin.Layout, template, item.Layout.Name, item.Layout.Readonly);
                    }
                }
            }
        }

        public void UnsubscribeEventHandlers()
        {
            Groups.ToList().ForEach(g => g.UnsubscribeEventHandlers());
        }

        public IEnumerable<string> GetNames()
        {
            var names = new List<string>();
            Groups.ToList().ForEach(g => names.AddRange(g.GetNames()));
            return names;
        }

        public IEnumerable<string> GetPageNames()
            => Groups.First(g => g.TemplateType == TemplateType.Page).GetNames(p => p.ItemNameEditable);

        public async Task AddAsync(TemplateOrigin templateOrigin, TemplateInfoViewModel template, string layoutName = null, bool isReadOnly = false)
        {
            if (template.Exclusions.Count() > 0)
            {
                var exlusionsTemplateNames = AllTemplates.Where(t => template.Exclusions.Select(e => e.Identity).Contains(t.Identity)).Select(t => t.Template.Name).Distinct();
                if (exlusionsTemplateNames.Count() > 0 )
                {
                    await ShowAddTemplateExclusionsNotificationAsync(template.Name, exlusionsTemplateNames);
                    return;
                }
            }

            if (template.Requirements.Count() > 0)
            {
                if (!AllTemplates.Any(t => template.Requirements.Select(r => r.Identity).Contains(t.Identity)))
                {
                    await ShowAddTemplateWithRequirementNotificationAsync(template.Name, template.Requirements.Select(r => r.Name));
                    return;
                }
            }

            if (template.IsGroupExclusiveSelection)
            {
                var collection = GetCollection(template.TemplateType);
                var exclusiveSelectionAddedTemplates = collection.Where(f => f.Group == template.Group).ToList();
                foreach (var exclusiveFeature in exclusiveSelectionAddedTemplates)
                {
                    await RemoveAsync(exclusiveFeature);
                }
            }

            foreach (var dependency in template.Template.Dependencies)
            {
                var dependencyTemplate = MainViewModel.Instance.GetTemplate(dependency);
                if (dependencyTemplate == null)
                {
                    // Case of hidden templates, it's not found on templat lists
                    dependencyTemplate = new TemplateInfoViewModel(dependency, _context);
                }

                await AddAsync(templateOrigin, dependencyTemplate);
            }

            template.IncreaseSelection();
            var savedTemplate = new SavedTemplateViewModel(template, templateOrigin, isReadOnly);
            var focus = false;
            if (!IsTemplateAdded(template) || template.MultipleInstance)
            {
                if (!string.IsNullOrEmpty(layoutName))
                {
                    savedTemplate.SetName(layoutName, true);
                }
                else
                {
                    if (template.ItemNameEditable)
                    {
                        savedTemplate.SetName(ValidationService.InferTemplateName(template.Name));
                        focus = true;
                    }
                    else
                    {
                        savedTemplate.SetName(template.Template.DefaultName);
                    }
                }

                AddToGroup(template.TemplateType, savedTemplate);
                UpdateHasItemsAddedByUser();
                BuildLicenses();
                CheckForMissingVersions();

                if (focus)
                {
                    savedTemplate.IsTextSelected = true;
                }
            }
        }

        public bool IsTemplateAdded(TemplateInfoViewModel template) => GetCollection(template.TemplateType).Any(t => t.Equals(template));

        public void ResetUserSelection()
        {
            HasItemsAddedByUser = false;
            _isInitialized = false;
            Groups.ToList().ForEach(g => g.Clear());
        }

        public UserSelection GetUserSelection()
        {
            var selection = new UserSelection(_context);

            var pages = Groups.First(g => g.TemplateType == TemplateType.Page).Items;
            selection.HomeName = pages.First().Name;
            selection.Pages.AddRange(pages.Select(i => i.ToUserSelectionItem()));

            var features = Groups.First(g => g.TemplateType == TemplateType.Feature).Items;
            selection.Features.AddRange(features.Select(i => i.ToUserSelectionItem()));

            var services = Groups.First(g => g.TemplateType == TemplateType.Service).Items;
            selection.Services.AddRange(services.Select(i => i.ToUserSelectionItem()));

            var tests = Groups.First(g => g.TemplateType == TemplateType.Testing).Items;
            selection.Testing.AddRange(tests.Select(i => i.ToUserSelectionItem()));
            return selection;
        }

        public async Task DeleteSavedTemplateAsync(SavedTemplateViewModel template)
        {
            var group = GetGroup(template.TemplateType);
            if (template.IsReadOnly)
            {
                await ShowReadOnlyNotificationAsync(template.Name);
                return;
            }

            int newIndex = group.Items.IndexOf(template) - 1;
            newIndex = newIndex >= 0 ? newIndex : 0;

            var requirements = AllTemplates.Where(t => t.Template.Requirements.Select(r => r.TemplateId).Contains(template.Identity));
            if (requirements != null && requirements.Any())
            {
                await ShowRemoveRequirementNotificationAsync(template.Name, requirements.Select(t => t.Name));
                return;
            }

            var dependencies = await RemoveAsync(template);
            if (dependencies != null && dependencies.Any())
            {
                await ShowDependencyNotificationAsync(template.Name, dependencies.Select(t => t.Name));
            }
            else if (group.Items.Any())
            {
                newIndex = newIndex < group.Items.Count ? newIndex : group.Items.Count - 1;
                group.SelectedItem = group.Items.ElementAt(newIndex);
                group.SelectedItem.IsFocused = true;
            }
        }

        private ObservableCollection<SavedTemplateViewModel> GetCollection(TemplateType templateType)
        {
            return Groups.First(g => g.TemplateType == templateType).Items;
        }

        private void AddToGroup(TemplateType templateType, SavedTemplateViewModel savedTemplate)
        {
            Func<SavedTemplateViewModel, bool> genGroupEqual = (SavedTemplateViewModel st) => st.GenGroup == savedTemplate.GenGroup;
            Func<SavedTemplateViewModel, bool> genGroupPrevious = (SavedTemplateViewModel st) => st.GenGroup < savedTemplate.GenGroup;

            int index = 0;
            var group = GetGroup(templateType);
            if (group.Items.Any(genGroupEqual))
            {
                index = group.Items.IndexOf(group.Items.Last(genGroupEqual)) + 1;
            }
            else if (group.Items.Any())
            {
                index = group.Items.IndexOf(group.Items.Last(genGroupPrevious)) + 1;
            }

            group.Insert(index, savedTemplate);
        }

        private void BuildLicenses()
        {
            var userSelection = GetUserSelection();
            var licenses = GenComposer.GetAllLicences(userSelection);
            LicensesService.SyncLicenses(licenses, Licenses);

            // Notify Licenses name to update the visibillity on the layout
            OnPropertyChanged(nameof(Licenses));
        }

        private void CheckForMissingVersions()
        {
            var requiredVersions = GenComposer.GetAllRequiredVersions(GetUserSelection());
            var missingVersions = new List<RequiredVersionInfo>();

            foreach (var requiredVersion in requiredVersions)
            {
                var requirementInfo = RequiredVersionService.GetVersionInfo(requiredVersion);
                var isInstalled = RequiredVersionService.Instance.IsVersionInstalled(requirementInfo);
                if (!isInstalled)
                {
                    missingVersions.Add(requirementInfo);
                }
            }

            if (missingVersions.Any())
            {
                var missingSdkVersions = missingVersions.Select(v => RequiredVersionService.GetRequirementDisplayName(v));

                var notification = Notification.Warning(string.Format(StringRes.NotificationMissingVersions, missingSdkVersions.Aggregate((i, j) => $"{i}, {j}")), Category.MissingVersion, TimerType.None);
                NotificationsControl.AddNotificationAsync(notification).FireAndForget();
            }
            else
            {
                NotificationsControl.CleanCategoryNotificationsAsync(Category.MissingVersion).FireAndForget();
            }
        }

        private async Task<IEnumerable<SavedTemplateViewModel>> RemoveAsync(SavedTemplateViewModel savedTemplate)
        {
            // Look if is there any templates that depends on item
            var dependencies = GetSavedTemplateDependencies(savedTemplate);
            if (!dependencies.Any())
            {
                var group = GetGroup(savedTemplate.TemplateType);
                if (group.Items.Contains(savedTemplate))
                {
                    group.Remove(savedTemplate);
                }

                if (savedTemplate.HasErrors)
                {
                    await NotificationsControl.CleanErrorNotificationsAsync(ErrorCategory.NamingValidation);
                    WizardStatus.Current.HasValidationErrors = false;
                }

                var template = MainViewModel.Instance.GetTemplate(savedTemplate.Template);
                template?.DecreaseSelection();

                await TryRemoveHiddenDependenciesAsync(savedTemplate);
                UpdateHasItemsAddedByUser();

                BuildLicenses();
                CheckForMissingVersions();

                AppHealth.Current.Telemetry.TrackEditSummaryItemAsync(EditItemActionEnum.Remove).FireAndForget();
            }

            return dependencies;
        }

        public UserSelectionGroup GetGroup(TemplateType templateType) => Groups.First(t => t.TemplateType == templateType);

        private async Task TryRemoveHiddenDependenciesAsync(SavedTemplateViewModel savedTemplate)
        {
            foreach (var identity in savedTemplate.Dependencies)
            {
                var dependency = AllTemplates.FirstOrDefault(f => f.Identity == identity.Identity);

                if (dependency != null)
                {
                    // If the template is not hidden we can not remove it because it could be added in wizard
                    if (dependency.IsHidden)
                    {
                        // Look if there are another saved template that depends on it.
                        // For example, if it's added two different chart pages, when remove the first one SampleDataService can not be removed, but if no saved templates use SampleDataService, it can be removed.
                        if (!AllTemplates.Any(sf => sf.Dependencies.Any(d => d.Identity == dependency.Identity)))
                        {
                            await RemoveAsync(dependency);
                        }
                    }
                }
            }
        }

        private IEnumerable<SavedTemplateViewModel> AllTemplates
        {
            get
            {
                foreach (var group in Groups)
                {
                    foreach (var item in group.Items)
                    {
                        yield return item;
                    }
                }
            }
        }

        private IEnumerable<SavedTemplateViewModel> GetSavedTemplateDependencies(SavedTemplateViewModel savedTemplate)
        {
            return AllTemplates.Where(p => p.Dependencies
                               .Any(d => d.Identity == savedTemplate.Identity));
        }

        private void UpdateHasItemsAddedByUser()
        {
            foreach (var template in AllTemplates)
            {
                if (template.TemplateOrigin == TemplateOrigin.UserSelection)
                {
                    HasItemsAddedByUser = true;
                    return;
                }
            }

            HasItemsAddedByUser = false;
        }

        private async Task ShowReadOnlyNotificationAsync(string name)
        {
            var message = string.Format(StringRes.NotificationRemoveError_ReadOnly, name);
            var notification = Notification.Warning(message, Category.RemoveTemplateValidation);
            await NotificationsControl.AddNotificationAsync(notification);
        }

        private async Task ShowAddTemplateExclusionsNotificationAsync(string name, IEnumerable<string> exludedTemplateNames)
        {
            var allExludedTemplateNames = ConcatTemplateNames(exludedTemplateNames);

            var message = string.Format(StringRes.NotificationAdditionError_Exclusion, name, allExludedTemplateNames);

            var notification = Notification.Warning(message, Category.AddTemplateValidation);
            await NotificationsControl.AddNotificationAsync(notification);
        }

        private async Task ShowAddTemplateWithRequirementNotificationAsync(string name, IEnumerable<string> requiredTemplateNames)
        {
            var allRequiredTemplateNames = ConcatTemplateNames(requiredTemplateNames);

            var message = string.Format(StringRes.NotificationAdditionError_Requirement, name, allRequiredTemplateNames);

            var notification = Notification.Warning(message, Category.AddTemplateValidation);
            await NotificationsControl.AddNotificationAsync(notification);
        }

        private async Task ShowRemoveRequirementNotificationAsync(string name, IEnumerable<string> requiredForNames)
        {
            var allRequiredForNames = ConcatTemplateNames(requiredForNames);

            var message = string.Format(StringRes.NotificationRemoveError_Requirement, name, allRequiredForNames);

            var notification = Notification.Warning(message, Category.RemoveTemplateValidation);
            await NotificationsControl.AddNotificationAsync(notification);
        }

        private async Task ShowDependencyNotificationAsync(string name, IEnumerable<string> dependencyNames)
        {
            var allDependencyNames = ConcatTemplateNames(dependencyNames);

            var message = string.Format(StringRes.NotificationRemoveError_Dependency, name, allDependencyNames);
            var notification = Notification.Warning(message, Category.RemoveTemplateValidation);
            await NotificationsControl.AddNotificationAsync(notification);
        }

        private string ConcatTemplateNames(IEnumerable<string> templateNames)
        {
            var formatedNames = string.Empty;
            foreach (var templateName in templateNames.Take(3))
            {
                formatedNames += $" **{templateName}**,";
            }

            formatedNames = formatedNames.Remove(formatedNames.Length - 1);
            if (templateNames.Count() > 3)
            {
                formatedNames += "...";
            }

            return formatedNames;
        }
    }
}
