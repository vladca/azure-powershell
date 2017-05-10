﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.Azure.Commands.Common.Authentication;
using Microsoft.Azure.Commands.Common.Authentication.Models;

namespace Microsoft.Azure.Commands.TrafficManager.Utilities
{
    using Management.TrafficManager;
    using Management.TrafficManager.Models;
    using Microsoft.Azure.Commands.ResourceManager.Common.Tags;
    using Models;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    public class TrafficManagerClient
    {
        public const string ProfileResourceLocation = "global";

        public Action<string> VerboseLogger { get; set; }

        public Action<string> ErrorLogger { get; set; }

        public TrafficManagerClient(AzureContext context)
            : this(AzureSession.ClientFactory.CreateArmClient<TrafficManagerManagementClient>(context, AzureEnvironment.Endpoint.ResourceManager))
        {
        }

        public TrafficManagerClient(ITrafficManagerManagementClient client)
        {
            this.TrafficManagerManagementClient = client;
        }

        public ITrafficManagerManagementClient TrafficManagerManagementClient { get; set; }

        public TrafficManagerProfile CreateTrafficManagerProfile(string resourceGroupName, string profileName, string profileStatus, string trafficRoutingMethod, string relativeDnsName, uint ttl, string monitorProtocol, uint monitorPort, string monitorPath, Hashtable tag)
        {
            Profile response = this.TrafficManagerManagementClient.Profiles.CreateOrUpdate(
                resourceGroupName,
                profileName,
                new Profile(name: profileName, location: TrafficManagerClient.ProfileResourceLocation)
                {
                    ProfileStatus = profileStatus,
                    TrafficRoutingMethod = trafficRoutingMethod,
                    DnsConfig = new DnsConfig
                    {
                        RelativeName = relativeDnsName,
                        Ttl = ttl
                    },
                    MonitorConfig = new MonitorConfig
                    {
                        Protocol = monitorProtocol,
                        Port = monitorPort,
                        Path = monitorPath
                    },
                    Tags = TagsConversionHelper.CreateTagDictionary(tag, validate: true),
                });

            return TrafficManagerClient.GetPowershellTrafficManagerProfile(resourceGroupName, profileName, response);
        }

        public TrafficManagerEndpoint CreateTrafficManagerEndpoint(string resourceGroupName, string profileName, string endpointType, string endpointName, string targetResourceId, string target, string endpointStatus, uint? weight, uint? priority, string endpointLocation, uint? minChildEndpoints, IList<string> geoMapping)
        {
            Endpoint response = this.TrafficManagerManagementClient.Endpoints.CreateOrUpdate(
                resourceGroupName,
                profileName,
                endpointType,
                endpointName,
                new Endpoint(name: endpointName, type: TrafficManagerEndpoint.ToSDKEndpointType(endpointType))
                {
                    EndpointLocation = endpointLocation,
                    EndpointStatus = endpointStatus,
                    GeoMapping = geoMapping,
                    MinChildEndpoints = minChildEndpoints,
                    Priority = priority,
                    Target = target,
                    TargetResourceId = targetResourceId,
                    Weight = weight,
                });

            return TrafficManagerClient.GetPowershellTrafficManagerEndpoint(response.Id, resourceGroupName, profileName, endpointType, endpointName, response);
        }

        public TrafficManagerProfile GetTrafficManagerProfile(string resourceGroupName, string profileName)
        {
            Profile response = this.TrafficManagerManagementClient.Profiles.Get(resourceGroupName, profileName);

            return TrafficManagerClient.GetPowershellTrafficManagerProfile(resourceGroupName, profileName, response);
        }

        public TrafficManagerEndpoint GetTrafficManagerEndpoint(string resourceGroupName, string profileName, string endpointType, string endpointName)
        {
            Endpoint response = this.TrafficManagerManagementClient.Endpoints.Get(resourceGroupName, profileName, endpointType, endpointName);

            return TrafficManagerClient.GetPowershellTrafficManagerEndpoint(
                response.Id,
                resourceGroupName,
                profileName,
                endpointType,
                endpointName,
                response);
        }

        public TrafficManagerProfile[] ListTrafficManagerProfiles(string resourceGroupName = null)
        {
            IEnumerable<Profile> response =
                resourceGroupName == null ?
                this.TrafficManagerManagementClient.Profiles.ListAll() :
                this.TrafficManagerManagementClient.Profiles.ListAllInResourceGroup(resourceGroupName);

            return response.Select(profile => TrafficManagerClient.GetPowershellTrafficManagerProfile(
                resourceGroupName ?? TrafficManagerClient.ExtractResourceGroupFromId(profile.Id),
                profile.Name,
                profile)).ToArray();
        }

        public TrafficManagerProfile SetTrafficManagerProfile(TrafficManagerProfile profile)
        {
            Profile profileToSet = profile.ToSDKProfile();

            Profile response = this.TrafficManagerManagementClient.Profiles.CreateOrUpdate(
                profile.ResourceGroupName,
                profile.Name,
                profileToSet
                );

            return TrafficManagerClient.GetPowershellTrafficManagerProfile(profile.ResourceGroupName, profile.Name, response);
        }

        public TrafficManagerEndpoint SetTrafficManagerEndpoint(TrafficManagerEndpoint endpoint)
        {
            Endpoint endpointToSet = endpoint.ToSDKEndpoint();

            Endpoint response = this.TrafficManagerManagementClient.Endpoints.CreateOrUpdate(
                endpoint.ResourceGroupName,
                endpoint.ProfileName,
                endpoint.Type,
                endpoint.Name,
                endpointToSet);

            return TrafficManagerClient.GetPowershellTrafficManagerEndpoint(
                endpoint.Id,
                endpoint.ResourceGroupName,
                endpoint.ProfileName,
                endpoint.Type,
                endpoint.Name,
                response);
        }

        public bool DeleteTrafficManagerProfile(TrafficManagerProfile profile)
        {
            // AzureOperationResponse response = 
            this.TrafficManagerManagementClient.Profiles.Delete(profile.ResourceGroupName, profile.Name);

            // return response.StatusCode.Equals(HttpStatusCode.OK);
            return true;
        }

        public bool DeleteTrafficManagerEndpoint(TrafficManagerEndpoint trafficManagerEndpoint)
        {
            // AzureOperationResponse response = 
            this.TrafficManagerManagementClient.Endpoints.Delete(
                trafficManagerEndpoint.ResourceGroupName,
                trafficManagerEndpoint.ProfileName,
                trafficManagerEndpoint.Type,
                trafficManagerEndpoint.Name);

            // return response.StatusCode.Equals(HttpStatusCode.OK);
            return true;
        }

        public bool EnableDisableTrafficManagerProfile(TrafficManagerProfile profile, bool shouldEnableProfileStatus)
        {
            profile.ProfileStatus = shouldEnableProfileStatus ? Constants.StatusEnabled : Constants.StatusDisabled;

            Profile sdkProfile = profile.ToSDKProfile();
            sdkProfile.DnsConfig = null;
            sdkProfile.Endpoints = null;
            sdkProfile.TrafficRoutingMethod = null;
            sdkProfile.MonitorConfig = null;

            Profile response = this.TrafficManagerManagementClient.Profiles.Update(profile.ResourceGroupName, profile.Name, sdkProfile);

            return true;
        }

        public bool EnableDisableTrafficManagerEndpoint(TrafficManagerEndpoint endpoint, bool shouldEnableEndpointStatus)
        {
            endpoint.EndpointStatus = shouldEnableEndpointStatus ? Constants.StatusEnabled : Constants.StatusDisabled;

            Endpoint sdkEndpoint = endpoint.ToSDKEndpointForPatch();
            sdkEndpoint.EndpointStatus = endpoint.EndpointStatus;

            Endpoint response = this.TrafficManagerManagementClient.Endpoints.Update(
                endpoint.ResourceGroupName,
                endpoint.ProfileName,
                endpoint.Type,
                endpoint.Name,
                sdkEndpoint);

            return true;
        }

        private static TrafficManagerProfile GetPowershellTrafficManagerProfile(string resourceGroupName, string profileName, Profile sdkProfile)
        {
            var profile = new TrafficManagerProfile
            {
                Id = sdkProfile.Id,
                Name = profileName,
                ResourceGroupName = resourceGroupName,
                ProfileStatus = sdkProfile.ProfileStatus,
                RelativeDnsName = sdkProfile.DnsConfig.RelativeName,
                Ttl = (uint)sdkProfile.DnsConfig.Ttl,
                TrafficRoutingMethod = sdkProfile.TrafficRoutingMethod,
                MonitorProtocol = sdkProfile.MonitorConfig.Protocol,
                MonitorPort = (uint)sdkProfile.MonitorConfig.Port,
                MonitorPath = sdkProfile.MonitorConfig.Path
            };

            if (sdkProfile.Endpoints != null)
            {
                profile.Endpoints = new List<TrafficManagerEndpoint>();

                foreach (Endpoint endpoint in sdkProfile.Endpoints)
                {
                    profile.Endpoints.Add(
                        GetPowershellTrafficManagerEndpoint(
                            endpoint.Id,
                            resourceGroupName,
                            profileName,
                            endpoint.Type,
                            endpoint.Name, 
                            endpoint));
                }
            }

            return profile;
        }

        private static string ExtractResourceGroupFromId(string id)
        {
            return id.Split('/')[4];
        }

        private static TrafficManagerEndpoint GetPowershellTrafficManagerEndpoint(string id, string resourceGroupName, string profileName, string endpointType, string endpointName, Endpoint sdkEndpoint)
        {
            return new TrafficManagerEndpoint
            {
                Id = id,
                ResourceGroupName = resourceGroupName,
                ProfileName = profileName,
                Name = endpointName,
                Type = endpointType,

                EndpointStatus = sdkEndpoint.EndpointStatus,
                EndpointMonitorStatus = sdkEndpoint.EndpointMonitorStatus,
                GeoMapping = sdkEndpoint.GeoMapping != null ? sdkEndpoint.GeoMapping.ToList() : null,
                Location = sdkEndpoint.EndpointLocation,
                MinChildEndpoints = (uint?)sdkEndpoint.MinChildEndpoints,
                Priority = (uint?)sdkEndpoint.Priority,
                Target = sdkEndpoint.Target,
                TargetResourceId = sdkEndpoint.TargetResourceId,
                Weight = (uint?)sdkEndpoint.Weight,
            };
        }
    }
}
