﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using Microsoft.Templates.Core;
using Microsoft.TemplateEngine.Abstractions;

using Xunit;
using Microsoft.Templates.Fakes;

namespace Microsoft.Templates.Test
{
    [Collection("BuildTemplateTestCollection")]
    [Trait("ExecutionSet", "BuildMVVMBasic")]
    public class BuildMVVMBasicProjectTests : BaseGenAndBuildTests
    {
        public BuildMVVMBasicProjectTests(BuildTemplatesTestFixture fixture)
            : base(fixture, null, "MVVMBasic")
        {
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic")]
        [Trait("Type", "BuildProjects")]
        public async Task BuildEmptyProjectAsync(string projectType, string framework, string platform, string language)
        {
            var (projectName, projectPath) = await GenerateEmptyProjectAsync(projectType, framework, platform, language);

            AssertBuildProjectAsync(projectPath, projectName, platform);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic")]
        [Trait("Type", "BuildProjects")]
        public async Task BuildEmptyProjectAndInferConfigAsync(string projectType, string framework, string platform, string language)
        {
            var (projectName, projectPath) = await GenerateEmptyProjectAsync(projectType, framework, platform, language);
            _fixture.BuildSolution(projectName, projectPath, platform);

            EnsureCanInferConfigInfo(projectType, framework, platform, projectPath);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic")]
        [Trait("Type", "BuildAllPagesAndFeatures")]
        public async Task BuildAllPagesAndFeaturesAsync(string projectType, string framework, string platform, string language)
        {
            var (projectName, projectPath) = await GenerateAllPagesAndFeaturesAsync(projectType, framework, platform, language);

            AssertBuildProjectAsync(projectPath, projectName, platform);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic")]
        [Trait("Type", "BuildAllPagesAndFeatures")]
        public async Task BuildAllPagesAndFeaturesThenRunTestsAsync(string projectType, string framework, string platform, string language)
        {
            var (projectName, projectPath) = await GenerateAllPagesAndFeaturesAsync(projectType, framework, platform, language);

            AssertBuildProjectThenRunTestsAsync(projectPath, projectName, platform);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic")]
        [Trait("Type", "BuildAllPagesAndFeatures")]
        public async Task BuildAllPagesAndFeaturesProjectNameValidationAsync(string projectType, string framework, string platform, string language)
        {

            Func<ITemplateInfo, bool> templateSelector =
                        t => (t.GetTemplateType() == TemplateType.Page || t.GetTemplateType() == TemplateType.Feature)
                        && (t.GetProjectTypeList().Contains(projectType) || t.GetProjectTypeList().Contains(All))
                        && t.GetFrontEndFrameworkList().Contains(framework)
                        && t.GetPlatform() == platform
                        && !t.GetIsHidden();

            var projectName = $"{ShortProjectType(projectType)}{CharactersThatMayCauseProjectNameIssues()}{ShortLanguageName(language)}";

            var projectPath = await AssertGenerateProjectAsync(projectName, projectType, framework, platform, language, templateSelector, BaseGenAndBuildFixture.GetDefaultName);

            AssertBuildProjectAsync(projectPath, projectName, platform);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic", ProgrammingLanguages.CSharp, Platforms.Uwp)]
        [Trait("Type", "BuildRandomNames")]
        [Trait("ExecutionSet", "Minimum")]
        [Trait("ExecutionSet", "BuildMinimum")]
        public async Task BuildAllPagesAndFeaturesRandomNamesCSAsync(string projectType, string framework, string platform, string language)
        {
            await BuildAllPagesAndFeaturesRandomNamesAsync(projectType, framework, platform, language);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic", ProgrammingLanguages.VisualBasic, Platforms.Uwp)]
        [Trait("Type", "BuildRandomNames")]
        [Trait("ExecutionSet", "Minimum")]
        [Trait("ExecutionSet", "BuildMinimumVB")]
        public async Task BuildAllPagesAndFeaturesRandomNamesVBAsync(string projectType, string framework, string platform, string language)
        {
            await BuildAllPagesAndFeaturesRandomNamesAsync(projectType, framework, platform, language);
        }

        private async Task BuildAllPagesAndFeaturesRandomNamesAsync(string projectType, string framework, string platform, string language)
        {
            Func<ITemplateInfo, bool> templateSelector =
                    t => (t.GetTemplateType() == TemplateType.Page || t.GetTemplateType() == TemplateType.Feature)
                    && (t.GetProjectTypeList().Contains(projectType) || t.GetProjectTypeList().Contains(All))
                    && t.GetFrontEndFrameworkList().Contains(framework)
                    && t.GetPlatform() == platform
                    && !t.GetIsHidden();

            var projectName = $"{ShortProjectType(projectType)}AllRandom{ShortLanguageName(language)}";

            var projectPath = await AssertGenerateProjectAsync(projectName, projectType, framework, platform, language, templateSelector, BaseGenAndBuildFixture.GetRandomName);

            AssertBuildProjectAsync(projectPath, projectName, platform);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic")]
        [Trait("Type", "BuildRightClick")]
        public async Task BuildEmptyProjectWithAllRightClickItemsAsync(string projectType, string framework, string platform, string language)
        {
            var projectName = $"{ShortProjectType(projectType)}AllRC{ShortLanguageName(language)}";

            var projectPath = await AssertGenerateRightClickAsync(projectName, projectType, framework, platform, language, true);

            AssertBuildProjectAsync(projectPath, projectName, platform);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetProjectTemplatesForBuild), "MVVMBasic")]
        [Trait("Type", "BuildRightClick")]
        public async Task BuildCompleteProjectWithAllRightClickItemsAsync(string projectType, string framework, string platform, string language)
        {
            var projectName = $"{ShortProjectType(projectType)}AllRC2{ShortLanguageName(language)}";

            var projectPath = await AssertGenerateRightClickAsync(projectName, projectType, framework, platform, language, false);

            AssertBuildProjectAsync(projectPath, projectName, platform);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetPageAndFeatureTemplatesForBuild), "MVVMBasic", ProgrammingLanguages.CSharp)]
        [Trait("Type", "BuildOneByOneMVVMBasic")]
        public async Task BuildMVVMBasicOneByOneItemsCSAsync(string itemName, string projectType, string framework, string platform, string itemId, string language)
        {
            var result = await AssertGenerationOneByOneAsync(itemName, projectType, framework, platform, itemId, language, false);

            AssertBuildProjectAsync(result.ProjectPath, result.ProjecName, platform);
        }

        [Theory]
        [MemberData(nameof(BaseGenAndBuildTests.GetPageAndFeatureTemplatesForBuild), "MVVMBasic", ProgrammingLanguages.VisualBasic)]
        [Trait("Type", "BuildOneByOneMVVMBasic")]
        public async Task BuildMVVMBasicOneByOneItemsVBAsync(string itemName, string projectType, string framework, string platform, string itemId, string language)
        {
            var result = await AssertGenerationOneByOneAsync(itemName, projectType, framework, platform, itemId, language, false);

            AssertBuildProjectAsync(result.ProjectPath, result.ProjecName, platform);
        }
    }
}
