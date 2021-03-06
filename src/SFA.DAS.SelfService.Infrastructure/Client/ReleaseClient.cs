using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;
using Microsoft.VisualStudio.Services.WebApi;
using SFA.DAS.SelfService.Core.Configuration;
using SFA.DAS.SelfService.Core.Entities;
using SFA.DAS.SelfService.Core.IReleases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SFA.DAS.SelfService.Infrastructure.Releases
{
    public class ReleaseClient : IReleaseClient
    {
        private readonly VssConnection _vssConnection;
        private readonly IOptions<VstsConfiguration> _configuration;

        public ReleaseClient(IOptions<VstsConfiguration> configuration)
        {
            _configuration = configuration;

            _vssConnection = SetConnection();
        }

        private VssConnection SetConnection()
        {
            VssCredentials creds = new VssBasicCredential(string.Empty, _configuration.Value.PatToken);

            return new VssConnection(new Uri(_configuration.Value.CollectionUri), creds); ;
        }

        public async Task<List<VstsReleaseDefinition>> GetReleases()
        {
            var client = _vssConnection.GetClient<ReleaseHttpClient>();

            var releases = await client.GetReleaseDefinitionsAsync(_configuration.Value.ProjectName, expand: ReleaseDefinitionExpands.Variables);

            var vstsReleases = releases
                .Select(r => new VstsReleaseDefinition
                {
                    Id = r.Id,
                    ReleaseName = r.Name
                }).ToList();

            return vstsReleases;
        }

        public async Task<VstsReleaseDefinition> GetRelease(int releaseDefinitionId)
        {
            var client = _vssConnection.GetClient<ReleaseHttpClient>();

            var release = await client.GetReleaseDefinitionAsync(_configuration.Value.ProjectName, releaseDefinitionId);

            var vstsRelease = new VstsReleaseDefinition
            {
                Id = release.Id,
                ReleaseName = release.Name
            };

            return vstsRelease;
        }

        public async Task<IList<VstsReleaseStatus>> CheckReleaseStatus(int releaseDefinitionId, int releaseId)
        {
            ReleaseHttpClient releaseClient = _vssConnection.GetClient<ReleaseHttpClient>();

            List<Deployment> deployments = await releaseClient.GetDeploymentsAsync(project: _configuration.Value.ProjectName, definitionId: releaseDefinitionId, top: 25);

            var deployment = deployments.Where(r => r.Release.Id == releaseId).OrderBy(x => x.ReleaseEnvironmentReference.Id).ToList();

            List<VstsReleaseStatus> vstsReleaseStatus = deployment.Select(x => new VstsReleaseStatus { Name = x.ReleaseEnvironmentReference.Name, ReleaseStatus = (VstsReleaseStatus.Status)x.DeploymentStatus }).ToList();

            return vstsReleaseStatus;
        }

        public async Task<VstsRelease> CreateRelease(int releaseDefinitionId, Dictionary<string, string> overrideParameters)
        {
            ReleaseHttpClient releaseClient = _vssConnection.GetClient<ReleaseHttpClient>();

            Dictionary<string, ConfigurationVariableValue> overRideReleaseLevelVariables = new Dictionary<string, ConfigurationVariableValue>();

            foreach (var overrideParameter in overrideParameters)
            {
                ConfigurationVariableValue overrideVaraible = new ConfigurationVariableValue();
                overrideVaraible.Value = overrideParameter.Value;
                overrideVaraible.IsSecret = false;
                overRideReleaseLevelVariables.Add(overrideParameter.Key, overrideVaraible);
            }

            return await CreateRelease(releaseClient, releaseDefinitionId, _configuration.Value.ProjectName, overRideReleaseLevelVariables);
        }

        public async Task<VstsRelease> CreateRelease(int releaseDefinitionId)
        {
            ReleaseHttpClient releaseClient = _vssConnection.GetClient<ReleaseHttpClient>();

            return await CreateRelease(releaseClient, releaseDefinitionId, _configuration.Value.ProjectName); ;
        }

        public async Task<VstsRelease> CreateRelease(ReleaseHttpClient releaseClient, int releaseDefinitionId, string projectName, Dictionary<string, ConfigurationVariableValue> overrideVaraibles = null)
        {
            ReleaseStartMetadata releaseStartMetaData = new ReleaseStartMetadata();
            releaseStartMetaData.DefinitionId = releaseDefinitionId;
            releaseStartMetaData.Description = "Release created by das-self-service";

            if (overrideVaraibles != null)
            {
                // If you want to override varaibles at release create time, 'AllowOverride' on variable should be set while creating RD.
                // You can override environment level variables using releaseStartMetaData.EnvironmentsMetadata.Variables.
                releaseStartMetaData.Variables = overrideVaraibles;
            }

            // Create  a release
            var release = await
                releaseClient.CreateReleaseAsync(project: projectName, releaseStartMetadata: releaseStartMetaData);

            return new VstsRelease
            {
                Id = release.Id,
                ReleaseName = release.Name,
                ReleaseDefininitionId = release.ReleaseDefinitionReference.Id,
                ReleaseEnvironments = release.Environments.Select(x => new VstsEnvironment { DefinitionId = x.DefinitionEnvironmentId, EnvironmentReleaseId = x.Id, Name = x.Name }).ToList()
            };
        }

        public async Task StartEnvironmentDeployment(VstsRelease vstsRelease, int releaseEnvironmentId)
        {
            ReleaseHttpClient releaseClient = _vssConnection.GetClient<ReleaseHttpClient>();

            ReleaseEnvironmentUpdateMetadata releaseEnvironmentUpdateMetadata = new ReleaseEnvironmentUpdateMetadata()
            {
                Status = EnvironmentStatus.InProgress
            };

            await releaseClient.UpdateReleaseEnvironmentAsync(releaseEnvironmentUpdateMetadata, _configuration.Value.ProjectName, vstsRelease.Id, releaseEnvironmentId);
        }
    }
}