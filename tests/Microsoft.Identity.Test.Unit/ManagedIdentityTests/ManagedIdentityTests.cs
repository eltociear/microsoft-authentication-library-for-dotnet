﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Cache;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.Identity.Client.Internal;
using Microsoft.Identity.Client.TelemetryCore.Internal.Events;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Test.Common;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.Identity.Test.Common.Core.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using static Microsoft.Identity.Test.Common.Core.Helpers.ManagedIdentityTestUtil;

namespace Microsoft.Identity.Test.Unit.ManagedIdentityTests
{

    [TestClass]
    public class ManagedIdentityTests : TestBase
    {
        internal const string Resource = "https://management.azure.com";
        internal const string ResourceDefaultSuffix = "https://management.azure.com/.default";
        internal const string AppServiceEndpoint = "http://127.0.0.1:41564/msi/token";
        internal const string ImdsEndpoint = "http://169.254.169.254/metadata/identity/oauth2/token";
        internal const string AzureArcEndpoint = "http://localhost:40342/metadata/identity/oauth2/token";
        internal const string CloudShellEndpoint = "http://localhost:40342/metadata/identity/oauth2/token";
        internal const string ServiceFabricEndpoint = "http://localhost:40342/metadata/identity/oauth2/token";

        [DataTestMethod]
        [DataRow("http://127.0.0.1:41564/msi/token/", Resource, ManagedIdentitySourceType.AppService)]
        [DataRow(AppServiceEndpoint, Resource, ManagedIdentitySourceType.AppService)]
        [DataRow(AppServiceEndpoint, ResourceDefaultSuffix, ManagedIdentitySourceType.AppService)]
        [DataRow(ImdsEndpoint, Resource, ManagedIdentitySourceType.IMDS)]
        [DataRow(null, Resource, ManagedIdentitySourceType.IMDS)]
        [DataRow(AzureArcEndpoint, Resource, ManagedIdentitySourceType.AzureArc)]
        [DataRow(AzureArcEndpoint, ResourceDefaultSuffix, ManagedIdentitySourceType.AzureArc)]
        [DataRow(CloudShellEndpoint, Resource, ManagedIdentitySourceType.CloudShell)]
        [DataRow(CloudShellEndpoint, ResourceDefaultSuffix, ManagedIdentitySourceType.CloudShell)]
        [DataRow(ServiceFabricEndpoint, Resource, ManagedIdentitySourceType.ServiceFabric)]
        [DataRow(ServiceFabricEndpoint, ResourceDefaultSuffix, ManagedIdentitySourceType.ServiceFabric)]
        public async Task ManagedIdentityHappyPathAsync(
            string endpoint,
            string scope,
            ManagedIdentitySourceType managedIdentitySource)
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(managedIdentitySource, endpoint);

                var mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .Build();

                httpManager.AddManagedIdentityMockHandler(
                    endpoint,
                    Resource,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    managedIdentitySource);

                var result = await mi.AcquireTokenForManagedIdentity(scope).ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);

                result = await mi.AcquireTokenForManagedIdentity(scope)
                    .ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.Cache, result.AuthenticationResultMetadata.TokenSource);
            }
        }

        [DataTestMethod]
        [DataRow(AppServiceEndpoint, ManagedIdentitySourceType.AppService, TestConstants.ClientId, UserAssignedIdentityId.ClientId)]
        [DataRow(AppServiceEndpoint, ManagedIdentitySourceType.AppService, TestConstants.MiResourceId, UserAssignedIdentityId.ResourceId)]
        [DataRow(ImdsEndpoint, ManagedIdentitySourceType.IMDS, TestConstants.ClientId, UserAssignedIdentityId.ClientId)]
        [DataRow(ImdsEndpoint, ManagedIdentitySourceType.IMDS, TestConstants.MiResourceId, UserAssignedIdentityId.ResourceId)]
        [DataRow(ServiceFabricEndpoint, ManagedIdentitySourceType.ServiceFabric, TestConstants.ClientId, UserAssignedIdentityId.ClientId)]
        [DataRow(ServiceFabricEndpoint, ManagedIdentitySourceType.ServiceFabric, TestConstants.MiResourceId, UserAssignedIdentityId.ResourceId)]
        public async Task ManagedIdentityUserAssignedHappyPathAsync(
            string endpoint,
            ManagedIdentitySourceType managedIdentitySource,
            string userAssignedId,
            UserAssignedIdentityId userAssignedIdentityId)
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(managedIdentitySource, endpoint);

                var mi = ManagedIdentityApplicationBuilder.Create(userAssignedId)
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .Build();

                httpManager.AddManagedIdentityMockHandler(
                    endpoint,
                    Resource,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    managedIdentitySource,
                    userAssignedClientIdOrResourceId: userAssignedId,
                    userAssignedIdentityId: userAssignedIdentityId);

                var result = await mi.AcquireTokenForManagedIdentity(Resource).ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);

                result = await mi.AcquireTokenForManagedIdentity(Resource)
                    .ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.Cache, result.AuthenticationResultMetadata.TokenSource);
            }
        }

        [DataTestMethod]
        [DataRow(AppServiceEndpoint, Resource, "https://graph.microsoft.com", ManagedIdentitySourceType.AppService)]
        [DataRow(ImdsEndpoint, Resource, "https://graph.microsoft.com", ManagedIdentitySourceType.IMDS)]
        [DataRow(AzureArcEndpoint, Resource, "https://graph.microsoft.com", ManagedIdentitySourceType.AzureArc)]
        [DataRow(CloudShellEndpoint, Resource, "https://graph.microsoft.com", ManagedIdentitySourceType.CloudShell)]
        [DataRow(ServiceFabricEndpoint, Resource, "https://graph.microsoft.com", ManagedIdentitySourceType.ServiceFabric)]
        public async Task ManagedIdentityDifferentScopesTestAsync(
            string endpoint,
            string scope,
            string anotherScope,
            ManagedIdentitySourceType managedIdentitySource)
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(managedIdentitySource, endpoint);

                var mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .Build();

                httpManager.AddManagedIdentityMockHandler(
                    endpoint,
                    Resource,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    managedIdentitySource);

                var result = await mi.AcquireTokenForManagedIdentity(scope).ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);

                // Acquire token for same scope
                result = await mi.AcquireTokenForManagedIdentity(scope)
                    .ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.Cache, result.AuthenticationResultMetadata.TokenSource);

                httpManager.AddManagedIdentityMockHandler(
                    endpoint,
                    anotherScope,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    managedIdentitySource);

                // Acquire token for another scope
                result = await mi.AcquireTokenForManagedIdentity(anotherScope).ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);
            }
        }

        [DataTestMethod]
        [DataRow(AppServiceEndpoint, Resource, ManagedIdentitySourceType.AppService)]
        [DataRow(ImdsEndpoint, Resource, ManagedIdentitySourceType.IMDS)]
        [DataRow(AzureArcEndpoint, Resource, ManagedIdentitySourceType.AzureArc)]
        [DataRow(CloudShellEndpoint, Resource, ManagedIdentitySourceType.CloudShell)]
        [DataRow(ServiceFabricEndpoint, Resource, ManagedIdentitySourceType.ServiceFabric)]
        public async Task ManagedIdentityForceRefreshTestAsync(
            string endpoint,
            string scope,
            ManagedIdentitySourceType managedIdentitySource)
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(managedIdentitySource, endpoint);

                var mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .Build();

                httpManager.AddManagedIdentityMockHandler(
                    endpoint,
                    Resource,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    managedIdentitySource);

                var result = await mi.AcquireTokenForManagedIdentity(scope).ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);

                // Acquire token from cache
                result = await mi.AcquireTokenForManagedIdentity(scope)
                    .ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.Cache, result.AuthenticationResultMetadata.TokenSource);

                httpManager.AddManagedIdentityMockHandler(
                    endpoint,
                    scope,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    managedIdentitySource);

                // Acquire token with force refresh
                result = await mi.AcquireTokenForManagedIdentity(scope).WithForceRefresh(true)
                    .ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);
            }
        }

        [DataTestMethod]
        [DataRow("user.read", ManagedIdentitySourceType.AppService, AppServiceEndpoint)]
        [DataRow("https://management.core.windows.net//user_impersonation", ManagedIdentitySourceType.AppService, AppServiceEndpoint)]
        [DataRow("s", ManagedIdentitySourceType.AppService, AppServiceEndpoint)]
        [DataRow("user.read", ManagedIdentitySourceType.IMDS, ImdsEndpoint)]
        [DataRow("https://management.core.windows.net//user_impersonation", ManagedIdentitySourceType.IMDS, ImdsEndpoint)]
        [DataRow("s", ManagedIdentitySourceType.IMDS, ImdsEndpoint)]
        [DataRow("user.read", ManagedIdentitySourceType.AzureArc, AzureArcEndpoint)]
        [DataRow("https://management.core.windows.net//user_impersonation", ManagedIdentitySourceType.AzureArc, AzureArcEndpoint)]
        [DataRow("s", ManagedIdentitySourceType.AzureArc, AzureArcEndpoint)]
        [DataRow("user.read", ManagedIdentitySourceType.CloudShell, CloudShellEndpoint)]
        [DataRow("https://management.core.windows.net//user_impersonation", ManagedIdentitySourceType.CloudShell, CloudShellEndpoint)]
        [DataRow("s", ManagedIdentitySourceType.CloudShell, CloudShellEndpoint)]
        [DataRow("user.read", ManagedIdentitySourceType.ServiceFabric, ServiceFabricEndpoint)]
        [DataRow("https://management.core.windows.net//user_impersonation", ManagedIdentitySourceType.ServiceFabric, ServiceFabricEndpoint)]
        [DataRow("s", ManagedIdentitySourceType.ServiceFabric, ServiceFabricEndpoint)]
        public async Task ManagedIdentityTestWrongScopeAsync(string resource, ManagedIdentitySourceType managedIdentitySource, string endpoint)
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(managedIdentitySource, endpoint);

                IManagedIdentityApplication mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager).Build();

                httpManager.AddManagedIdentityMockHandler(endpoint, resource, MockHelpers.GetMsiErrorResponse(),
                    managedIdentitySource, statusCode: HttpStatusCode.InternalServerError);
                httpManager.AddManagedIdentityMockHandler(endpoint, resource, MockHelpers.GetMsiErrorResponse(),
                    managedIdentitySource, statusCode: HttpStatusCode.InternalServerError);

                MsalServiceException ex = await Assert.ThrowsExceptionAsync<MsalServiceException>(async () =>
                    await mi.AcquireTokenForManagedIdentity(resource)
                    .ExecuteAsync().ConfigureAwait(false)).ConfigureAwait(false);

                Assert.IsNotNull(ex);
                Assert.AreEqual(MsalError.ManagedIdentityRequestFailed, ex.ErrorCode);
            }
        }

        [DataTestMethod]
        [DataRow(ManagedIdentitySourceType.AppService, AppServiceEndpoint)]
        [DataRow(ManagedIdentitySourceType.IMDS, ImdsEndpoint)]
        [DataRow(ManagedIdentitySourceType.AzureArc, AzureArcEndpoint)]
        [DataRow(ManagedIdentitySourceType.CloudShell, CloudShellEndpoint)]
        [DataRow(ManagedIdentitySourceType.ServiceFabric, ServiceFabricEndpoint)]
        public async Task ManagedIdentityErrorResponseNoPayloadTestAsync(ManagedIdentitySourceType managedIdentitySource, string endpoint)
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(managedIdentitySource, endpoint);

                IManagedIdentityApplication mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager).Build();

                httpManager.AddManagedIdentityMockHandler(endpoint, "scope", "",
                    managedIdentitySource, statusCode: HttpStatusCode.InternalServerError);
                httpManager.AddManagedIdentityMockHandler(endpoint, "scope", "",
                    managedIdentitySource, statusCode: HttpStatusCode.InternalServerError);

                MsalServiceException ex = await Assert.ThrowsExceptionAsync<MsalServiceException>(async () =>
                    await mi.AcquireTokenForManagedIdentity("scope")
                    .ExecuteAsync().ConfigureAwait(false)).ConfigureAwait(false);

                Assert.IsNotNull(ex);
                Assert.AreEqual(MsalError.ManagedIdentityRequestFailed, ex.ErrorCode);
                Assert.AreEqual(MsalErrorMessage.ManagedIdentityNoResponseReceived, ex.Message);
            }
        }

        [DataTestMethod]
        [DataRow(ManagedIdentitySourceType.AppService, AppServiceEndpoint)]
        [DataRow(ManagedIdentitySourceType.IMDS, ImdsEndpoint)]
        [DataRow(ManagedIdentitySourceType.AzureArc, AzureArcEndpoint)]
        [DataRow(ManagedIdentitySourceType.CloudShell, CloudShellEndpoint)]
        [DataRow(ManagedIdentitySourceType.ServiceFabric, ServiceFabricEndpoint)]
        public async Task AppServiceNullResponseAsync(ManagedIdentitySourceType managedIdentitySource, string endpoint)
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(managedIdentitySource, endpoint);

                IManagedIdentityApplication mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager).Build();

                httpManager.AddManagedIdentityMockHandler(
                    endpoint,
                    Resource,
                    "",
                    managedIdentitySource,
                    statusCode: HttpStatusCode.OK);

                MsalServiceException ex = await Assert.ThrowsExceptionAsync<MsalServiceException>(async () =>
                    await mi.AcquireTokenForManagedIdentity(Resource)
                    .ExecuteAsync().ConfigureAwait(false)).ConfigureAwait(false);

                Assert.IsNotNull(ex);
                Assert.AreEqual(MsalError.ManagedIdentityRequestFailed, ex.ErrorCode);
                Assert.AreEqual(MsalErrorMessage.ManagedIdentityInvalidResponse, ex.Message);
            }
        }

        [TestMethod] 
        public async Task SystemAssignedManagedIdentityApiIdTestAsync()
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(ManagedIdentitySourceType.AppService, AppServiceEndpoint);

                var mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .Build();

                httpManager.AddManagedIdentityMockHandler(
                    AppServiceEndpoint,
                    Resource,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    ManagedIdentitySourceType.AppService);

                var builder = mi.AcquireTokenForManagedIdentity(Resource);
                var result = await builder.ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);

                Assert.AreEqual(ApiEvent.ApiIds.AcquireTokenForSystemAssignedManagedIdentity, builder.CommonParameters.ApiId);
            }
        }

        [TestMethod]
        public async Task UserAssignedManagedIdentityApiIdTestAsync()
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(ManagedIdentitySourceType.AppService, AppServiceEndpoint);

                var mi = ManagedIdentityApplicationBuilder.Create(TestConstants.ClientId)
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .Build();

                httpManager.AddManagedIdentityMockHandler(
                    AppServiceEndpoint,
                    Resource,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    ManagedIdentitySourceType.AppService,
                    userAssignedClientIdOrResourceId: TestConstants.ClientId,
                    userAssignedIdentityId: UserAssignedIdentityId.ClientId);

                var builder = mi.AcquireTokenForManagedIdentity(Resource);
                var result = await builder.ExecuteAsync().ConfigureAwait(false);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);

                Assert.AreEqual(ApiEvent.ApiIds.AcquireTokenForUserAssignedManagedIdentity, builder.CommonParameters.ApiId);
            }
        }

        [TestMethod]
        public async Task ManagedIdentityCacheTestAsync()
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(ManagedIdentitySourceType.AppService, AppServiceEndpoint);

                var mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .BuildConcrete();
                CancellationTokenSource cts = new CancellationTokenSource();
                var cancellationToken = cts.Token;

                var appTokenCacheRecoder = mi.AppTokenCache.RecordAccess((args) =>
                {
                    Assert.AreEqual(Constants.ManagedIdentityDefaultTenant, args.RequestTenantId);
                    Assert.AreEqual(Constants.ManagedIdentityDefaultClientId, args.ClientId);
                    Assert.IsNull(args.Account);
                    Assert.IsTrue(args.IsApplicationCache);
                    Assert.AreEqual(cancellationToken, args.CancellationToken);
                });

                httpManager.AddManagedIdentityMockHandler(
                    AppServiceEndpoint,
                    Resource,
                    MockHelpers.GetMsiSuccessfulResponse(),
                    ManagedIdentitySourceType.AppService);

                var result = await mi.AcquireTokenForManagedIdentity(Resource).ExecuteAsync(cancellationToken).ConfigureAwait(false);

                appTokenCacheRecoder.AssertAccessCounts(1, 1);
            }
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public async Task ManagedIdentityExpiresOnTestAsync(int hoursToAdd)
        {
            AuthenticationResult result;

            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(ManagedIdentitySourceType.AppService, AppServiceEndpoint);

                var mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .Build();

                httpManager.AddManagedIdentityMockHandler(
                    AppServiceEndpoint,
                    Resource,
                    MockHelpers.GetMsiSuccessfulResponse(hoursToAdd),
                    ManagedIdentitySourceType.AppService);

                AcquireTokenForManagedIdentityParameterBuilder builder = mi.AcquireTokenForManagedIdentity(Resource);

                if (hoursToAdd == 0)
                {
                    MsalClientException ex = await AssertException.TaskThrowsAsync<MsalClientException>(
                        () => builder.ExecuteAsync()).ConfigureAwait(false);

                    Assert.AreEqual(ex.ErrorCode, "invalid_token_provider_response_value");
                    return;
                }
                else
                {
                    result = await builder.ExecuteAsync().ConfigureAwait(false);
                }

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AccessToken);
                Assert.AreEqual(TokenSource.IdentityProvider, result.AuthenticationResultMetadata.TokenSource);

                Assert.AreEqual(ApiEvent.ApiIds.AcquireTokenForSystemAssignedManagedIdentity, builder.CommonParameters.ApiId);

                switch (hoursToAdd)
                {
                    case 0:
                    case 1:
                    case 2:
                        Assert.IsFalse(result.AuthenticationResultMetadata.RefreshOn.HasValue);
                        break;
                    case 3:
                        Assert.IsTrue(result.AuthenticationResultMetadata.RefreshOn.HasValue);
                        break;
                }
            }
        }

        [TestMethod]
        public async Task ManagedIdentityRefreshInTestAsync()
        {
            using (new EnvVariableContext())
            using (var httpManager = new MockHttpManager())
            {
                SetEnvironmentVariables(ManagedIdentitySourceType.AppService, AppServiceEndpoint);

                Trace.WriteLine("1. Setup an app with a token cache with one AT");

                var mi = ManagedIdentityApplicationBuilder.Create()
                    .WithExperimentalFeatures()
                    .WithHttpManager(httpManager)
                    .BuildConcrete();
                
                httpManager.AddManagedIdentityMockHandler(
                        AppServiceEndpoint,
                        Resource,
                        MockHelpers.GetMsiSuccessfulResponse(3),
                        ManagedIdentitySourceType.AppService);

                AuthenticationResult result = await mi.AcquireTokenForManagedIdentity(Resource)
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                Trace.WriteLine("2. Configure AT so that it shows it needs to be refreshed");
                var refreshOn = UpdateATWithRefreshOn(mi.AppTokenCacheInternal.Accessor).RefreshOn;
                TokenCacheAccessRecorder cacheAccess = mi.AppTokenCache.RecordAccess();

                // Expire token
                TokenCacheHelper.ExpireAllAccessTokens(mi.AppTokenCacheInternal);
                
                Trace.WriteLine("3. Configure MSI to respond with valid token");
                httpManager.AddMsiMocks(TokenResponseType.Valid_ManagedIdentityResponse, AppServiceEndpoint);

                // Act
                Trace.WriteLine("4. ATM - should perform an RT refresh");
                result = await mi.AcquireTokenForManagedIdentity(Resource)
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                // Assert
                YieldTillSatisfied(() => httpManager.QueueSize == 0);
                Assert.IsNotNull(result);
                //Assert.AreEqual(0, httpManager.QueueSize,
                //    "MSAL should have refreshed the token because the original AT was marked for refresh");
                //cacheAccess.WaitTo_AssertAcessCounts(1, 1);
                Assert.IsTrue(result.AuthenticationResultMetadata.CacheRefreshReason == CacheRefreshReason.ProactivelyRefreshed);
                //Assert.IsTrue(result.AuthenticationResultMetadata.RefreshOn == refreshOn);

                result = await mi.AcquireTokenForManagedIdentity(Resource)
                    .ExecuteAsync()
                    .ConfigureAwait(false);
                Assert.IsTrue(result.AuthenticationResultMetadata.CacheRefreshReason == CacheRefreshReason.NotApplicable);



            }

            //////////////////////////////////////
            ///
            /// 
            //using (new EnvVariableContext())
            //using (var httpManager = new MockHttpManager())
            //{
            //    SetEnvironmentVariables(ManagedIdentitySourceType.AppService, AppServiceEndpoint);

            //    var mi = ManagedIdentityApplicationBuilder.Create()
            //        .WithExperimentalFeatures()
            //        .WithHttpManager(httpManager)
            //        .BuildConcrete();
            //    CancellationTokenSource cts = new CancellationTokenSource();
            //    var cancellationToken = cts.Token;


            //    var appTokenCacheRecoder = mi.AppTokenCache.RecordAccess((args) =>
            //    {
            //        Assert.AreEqual(Constants.ManagedIdentityDefaultTenant, args.RequestTenantId);
            //        Assert.AreEqual(Constants.ManagedIdentityDefaultClientId, args.ClientId);
            //        Assert.IsNull(args.Account);
            //        Assert.IsTrue(args.IsApplicationCache);
            //        Assert.AreEqual(cancellationToken, args.CancellationToken);
            //    });

            //    httpManager.AddManagedIdentityMockHandler(
            //        AppServiceEndpoint,
            //        Resource,
            //        MockHelpers.GetMsiSuccessfulResponse(3),
            //        ManagedIdentitySourceType.AppService);

            //    var result = await mi.AcquireTokenForManagedIdentity(Resource).ExecuteAsync(cancellationToken).ConfigureAwait(false);

            //    var refreshOn = UpdateATWithRefreshOn(mi.AppTokenCacheInternal.Accessor);


            //    YieldTillSatisfied(() => httpManager.QueueSize == 0);
            //    Assert.IsNotNull(result);
            //    Assert.AreEqual(0, httpManager.QueueSize,
            //        "MSAL should have refreshed the token because the original AT was marked for refresh");
            //    appTokenCacheRecoder.WaitTo_AssertAcessCounts(1, 1);

            //    var tokens = mi.AppTokenCacheInternal.Accessor.GetAllAccessTokens();



            //    result = await mi.AcquireTokenForManagedIdentity(Resource).ExecuteAsync(cancellationToken).ConfigureAwait(false);



            //    //appTokenCacheRecoder.AssertAccessCounts(1, 1);

            //    //cacheAccess.WaitTo_AssertAcessCounts(1, 1);
            //    Assert.IsTrue(result.AuthenticationResultMetadata.CacheRefreshReason == CacheRefreshReason.ProactivelyRefreshed);
            //    //Assert.IsTrue(result.AuthenticationResultMetadata.RefreshOn == refreshOn);


            //}


            ///
            //////////////////////////////////////////////////
        }
        private bool YieldTillSatisfied(Func<bool> func, int maxTimeInMilliSec = 30000)
        {
            int iCount = maxTimeInMilliSec / 100;
            while (iCount > 0)
            {
                if (func())
                {
                    return true;
                }
                Thread.Yield();
                Thread.Sleep(100);
                iCount--;
            }

            return false;
        }

        private static MsalAccessTokenCacheItem UpdateATWithRefreshOn(
            ITokenCacheAccessor accessor,
            DateTimeOffset? refreshOn = null,
            bool expired = false)
        {
            MsalAccessTokenCacheItem atItem = accessor.GetAllAccessTokens().Single();

            refreshOn = refreshOn ?? DateTimeOffset.UtcNow - TimeSpan.FromMinutes(30);

            atItem = atItem.WithRefreshOn(refreshOn);

            Assert.IsTrue(atItem.ExpiresOn > DateTime.UtcNow + TimeSpan.FromMinutes(10));

            if (expired)
            {
                atItem = atItem.WithExpiresOn(DateTime.UtcNow - TimeSpan.FromMinutes(1));
            }

            accessor.SaveAccessToken(atItem);

            return atItem;
        }
    }
}
