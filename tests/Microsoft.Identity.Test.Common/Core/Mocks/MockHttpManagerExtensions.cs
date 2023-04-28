﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.Instance.Discovery;
using Microsoft.Identity.Client.Utils;
using Microsoft.Identity.Test.Common.Core.Helpers;
using Microsoft.Identity.Test.Unit;
using static Microsoft.Identity.Test.Common.Core.Helpers.ManagedIdentityTestUtil;

namespace Microsoft.Identity.Test.Common.Core.Mocks
{
    internal static class MockHttpManagerExtensions
    {
        public static MockHttpMessageHandler AddInstanceDiscoveryMockHandler(
            this MockHttpManager httpManager, 
            string authority = TestConstants.AuthorityCommonTenant, 
            Uri customDiscoveryEndpoint = null, 
            string instanceMetadataContent = null)
        {
            Uri authorityURI = new Uri(authority);

            string discoveryEndpoint;

            if (customDiscoveryEndpoint == null)
            {
                string discoveryHost = KnownMetadataProvider.IsKnownEnvironment(authorityURI.Host)
                                           ? authorityURI.Host
                                           : AadAuthority.DefaultTrustedHost;

                discoveryEndpoint = UriBuilderExtensions.GetHttpsUriWithOptionalPort($"https://{discoveryHost}/common/discovery/instance", authorityURI.Port);
            }
            else
            {
                discoveryEndpoint = customDiscoveryEndpoint.AbsoluteUri;
            }

            return httpManager.AddMockHandler(
                MockHelpers.CreateInstanceDiscoveryMockHandler(
                    discoveryEndpoint, 
                    instanceMetadataContent ?? TestConstants.DiscoveryJsonResponse));
        }

        public static MockHttpMessageHandler AddWsTrustMockHandler(this MockHttpManager httpManager)
        {
            MockHttpMessageHandler wsTrustHandler = new MockHttpMessageHandler()
            {
                ExpectedUrl = "https://login.microsoftonline.com/common/userrealm/username",
                ExpectedMethod = HttpMethod.Get,
                ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ \"ver\":\"1.0\",\"account_type\":\"Managed\",\"domain_name\":\"domain.onmicrosoft.com\",\"cloud_instance_name\":\"microsoftonline.com\",\"cloud_audience_urn\":\"urn:federation:MicrosoftOnline\"}")
                }
            };

            return httpManager.AddMockHandler(wsTrustHandler);
        }

        public static MockHttpMessageHandler AddResponseMockHandlerForPost(
            this MockHttpManager httpManager,
            HttpResponseMessage responseMessage,
            IDictionary<string, string> bodyParameters = null,
            IDictionary<string, string> queryParameters = null)
        {
            return httpManager.AddMockHandler(
                new MockHttpMessageHandler()
                {
                    ExpectedMethod = HttpMethod.Post,
                    ExpectedPostData = bodyParameters,
                    ExpectedQueryParams = queryParameters,
                    ResponseMessage = responseMessage
                });
        }

        public static MockHttpMessageHandler AddFailureTokenEndpointResponse(
           this MockHttpManager httpManager,
           string error,
           string authority = TestConstants.AuthorityCommonTenant, 
           string correlationId = null)
        {
            var handler = new MockHttpMessageHandler()
            {
                ExpectedUrl = authority + "oauth2/v2.0/token",
                ExpectedMethod = HttpMethod.Post,
                ResponseMessage = MockHelpers.CreateFailureTokenResponseMessage(
                    error, 
                    correlationId: correlationId)
            };
            httpManager.AddMockHandler(handler);
            return handler;
        }

        public static MockHttpMessageHandler AddSuccessTokenResponseMockHandlerForPost(
            this MockHttpManager httpManager,
            string authority = TestConstants.AuthorityCommonTenant,
            IDictionary<string, string> bodyParameters = null,
            IDictionary<string, string> queryParameters = null,
            bool foci = false, 
            HttpResponseMessage responseMessage = null,
            IDictionary<string, string> expectedHttpHeaders = null)
        {
            var handler = new MockHttpMessageHandler()
            {
                ExpectedUrl = authority + "oauth2/v2.0/token",
                ExpectedMethod = HttpMethod.Post,
                ExpectedPostData = bodyParameters,
                ExpectedQueryParams = queryParameters,
                ResponseMessage = responseMessage ?? MockHelpers.CreateSuccessTokenResponseMessage(foci),
                ExpectedRequestHeaders = expectedHttpHeaders
            };
            httpManager.AddMockHandler(handler);
            return handler;
        }

        public static void AddSuccessTokenResponseMockHandlerForGet(
            this MockHttpManager httpManager,
            IDictionary<string, string> bodyParameters = null,
            IDictionary<string, string> queryParameters = null)
        {
            httpManager.AddMockHandler(
                new MockHttpMessageHandler()
                {
                    ExpectedMethod = HttpMethod.Get,
                    ExpectedPostData = bodyParameters,
                    ExpectedQueryParams = queryParameters,
                    ResponseMessage = MockHelpers.CreateSuccessTokenResponseMessage()
                });
        }

        public static HttpResponseMessage AddResiliencyMessageMockHandler(
            this MockHttpManager httpManager,
            HttpMethod httpMethod,
            HttpStatusCode httpStatusCode, 
            int? retryAfter = null)
        {
            var response = MockHelpers.CreateServerErrorMessage(httpStatusCode, retryAfter);
            httpManager.AddMockHandler(
                new MockHttpMessageHandler()
                {
                    ExpectedMethod = httpMethod,
                    ResponseMessage = response
                });
            return response;
        }

        public static void AddRequestTimeoutResponseMessageMockHandler(this MockHttpManager httpManager, HttpMethod httpMethod)
        {
            httpManager.AddMockHandler(
                new MockHttpMessageHandler()
                {
                    ExpectedMethod = httpMethod,
                    ResponseMessage = MockHelpers.CreateRequestTimeoutResponseMessage(),
                    ExceptionToThrow = new TaskCanceledException("request timed out")
                });
        }

        public static void AddMockHandlerContentNotFound(this MockHttpManager httpManager, HttpMethod httpMethod, string url = "")
        {
            httpManager.AddMockHandler(
                new MockHttpMessageHandler()
                {
                    ExpectedUrl = url,
                    ExpectedMethod = httpMethod,
                    ResponseMessage = new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent("Not found")
                    }
                });
        }

        public static MockHttpMessageHandler AddMockHandlerSuccessfulClientCredentialTokenResponseMessage(
            this MockHttpManager httpManager, 
            string token = "header.payload.signature", 
            string expiresIn = "3599",
            string tokenType = "Bearer",
            IList<string> unexpectedHttpHeaders = null)
        {
            var handler = new MockHttpMessageHandler()
            {
                ExpectedMethod = HttpMethod.Post,
                ResponseMessage = MockHelpers.CreateSuccessfulClientCredentialTokenResponseMessage(token, expiresIn, tokenType),
                UnexpectedRequestHeaders = unexpectedHttpHeaders
            };

            httpManager.AddMockHandler(handler);

            return handler;
        }

        public static MockHttpMessageHandler AddMockHandlerForThrottledResponseMessage(
            this MockHttpManager httpManager)
        {
            var handler = new MockHttpMessageHandler()
            {
                ExpectedMethod = HttpMethod.Post,
                ResponseMessage = MockHelpers.CreateTooManyRequestsNonJsonResponse()
            };

            httpManager.AddMockHandler(handler);

            return handler;
        }

        public static void AddFailingRequest(this MockHttpManager httpManager, Exception exceptionToThrow)
        {
            httpManager.AddMockHandler(
                new MockHttpMessageHandler
                {
                    ExpectedMethod = HttpMethod.Get,
                    ResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("Foo")
                    },
                    ExceptionToThrow = new InvalidOperationException("Error")
                });
        }

        public static void AddAdfs2019MockHandler(this MockHttpManager httpManager)
        {
            httpManager.AddMockHandler(
                new MockHttpMessageHandler
                {
                    ExpectedMethod = HttpMethod.Get,
                    ExpectedUrl = "https://fs.contoso.com/.well-known/webfinger",
                    ExpectedQueryParams = new Dictionary<string, string>
                    {
                            {"resource", "https://fs.contoso.com"},
                            {"rel", "http://schemas.microsoft.com/rel/trusted-realm"}
                    },
                    ResponseMessage = MockHelpers.CreateSuccessWebFingerResponseMessage("https://fs.contoso.com")
                });

            httpManager.AddMockHandler(new MockHttpMessageHandler
            {
                ExpectedMethod = HttpMethod.Post,
                ResponseMessage = MockHelpers.CreateAdfsSuccessTokenResponseMessage()
            });
        }

        public static MockHttpMessageHandler AddAllMocks(this MockHttpManager httpManager, TokenResponseType aadResponse)
        {
            httpManager.AddInstanceDiscoveryMockHandler();
            return AddTokenResponse(httpManager, aadResponse);
        }

        public static MockHttpMessageHandler AddMsiMocks(this MockHttpManager httpManager, TokenResponseType managedIdentityresponse, string endpoint)
        {
            return MsiTokenResponse(httpManager, managedIdentityresponse, endpoint);
        }

        public static MockHttpMessageHandler AddTokenResponse(
            this MockHttpManager httpManager, 
            TokenResponseType responseType, 
            IDictionary<string, string> expectedRequestHeaders = null)
        {
            HttpResponseMessage responseMessage;

            switch (responseType)
            {
                case TokenResponseType.Valid_UserFlows:
                    responseMessage = MockHelpers.CreateSuccessTokenResponseMessage(
                       TestConstants.Uid,
                       TestConstants.DisplayableId,
                       TestConstants.s_scope.ToArray());
                   
                    break;
                case TokenResponseType.Valid_ClientCredentials:
                    responseMessage = MockHelpers.CreateSuccessfulClientCredentialTokenResponseMessage();

                    break;
                case TokenResponseType.Invalid_AADUnavailable503:
                    responseMessage = MockHelpers.CreateFailureMessage(
                            System.Net.HttpStatusCode.ServiceUnavailable, "service down");
                   
                    break;
                case TokenResponseType.InvalidGrant:
                    responseMessage = MockHelpers.CreateInvalidGrantTokenResponseMessage();                   
                    break;
                case TokenResponseType.InvalidClient:                    

                    responseMessage = MockHelpers.CreateInvalidClientResponseMessage();
                    break;
                default:
                    throw new NotImplementedException();
            }

            var responseHandler = new MockHttpMessageHandler()
            {
                ExpectedMethod = HttpMethod.Post,
                ExpectedRequestHeaders = expectedRequestHeaders,
                ResponseMessage = responseMessage, 
            };
            httpManager.AddMockHandler(responseHandler);

            return responseHandler;
        }

        public static MockHttpMessageHandler MsiTokenResponse(
            this MockHttpManager httpManager,
            TokenResponseType responseType,
            string expectedUrl)
        {
            HttpResponseMessage responseMessage;

            switch (responseType)
            {
                case TokenResponseType.Valid_ManagedIdentityResponse:
                    responseMessage = MockHelpers.CreateSuccessfulMsiResponseMessage();
                    break;

                default:
                    throw new NotImplementedException();
            }

            var responseHandler = new MockHttpMessageHandler()
            {
                ExpectedMethod = HttpMethod.Get,
                ExpectedUrl = expectedUrl,
                ResponseMessage = responseMessage,
            };

            httpManager.AddMockHandler(responseHandler);

            return responseHandler;
        }

        public static HttpResponseMessage AddTokenErrorResponse(
            this MockHttpManager httpManager, 
            string error, 
            HttpStatusCode? customStatusCode)
        {
            var responseMessage = MockHelpers.CreateFailureTokenResponseMessage(error, customStatusCode: customStatusCode);
            var handler = new MockHttpMessageHandler()
            {
                ExpectedMethod = HttpMethod.Post,
                ResponseMessage = responseMessage
            };
            httpManager.AddMockHandler(handler);
            return responseMessage;
        }

        public static void AddRegionDiscoveryMockHandler(
            this MockHttpManager httpManager,
            string response)
        {
            httpManager.AddMockHandler(
                    new MockHttpMessageHandler
                    {
                        ExpectedMethod = HttpMethod.Get,
                        ExpectedUrl = "http://169.254.169.254/metadata/instance/compute/location",
                        ExpectedRequestHeaders = new Dictionary<string, string>
                         {
                            {"Metadata", "true"}
                         },
                        ResponseMessage = MockHelpers.CreateSuccessResponseMessage(response)
                    });
        }

        public static void AddManagedIdentityMockHandler(
            this MockHttpManager httpManager,
            string expectedUrl,
            string resource,
            string response,
            ManagedIdentitySourceType managedIdentitySourceType,
            string userAssignedClientIdOrResourceId = null,
            UserAssignedIdentityId userAssignedIdentityId = UserAssignedIdentityId.None,
            HttpStatusCode statusCode = HttpStatusCode.OK
            )
        {
            HttpResponseMessage responseMessage = new HttpResponseMessage(statusCode);
            HttpContent content = new StringContent(response);
            responseMessage.Content = content;

            MockHttpMessageHandler httpMessageHandler = BuildMockHandlerForManagedIdentitySource(managedIdentitySourceType, resource);

            if (userAssignedIdentityId == UserAssignedIdentityId.ClientId)
            {
                httpMessageHandler.ExpectedQueryParams.Add("client_id", userAssignedClientIdOrResourceId);
            }

            if (userAssignedIdentityId == UserAssignedIdentityId.ResourceId)
            {
                httpMessageHandler.ExpectedQueryParams.Add("mi_res_id", userAssignedClientIdOrResourceId);
            }

            httpMessageHandler.ResponseMessage = responseMessage;
            httpMessageHandler.ExpectedUrl = expectedUrl;

            httpManager.AddMockHandler(httpMessageHandler);
        }

            
        private static MockHttpMessageHandler BuildMockHandlerForManagedIdentitySource(ManagedIdentitySourceType managedIdentitySourceType, string resource)
        {
            MockHttpMessageHandler httpMessageHandler = new MockHttpMessageHandler();
            IDictionary<string, string> expectedQueryParams = new Dictionary<string, string>();
            IDictionary<string, string> expectedRequestHeaders = new Dictionary<string, string>();

            switch (managedIdentitySourceType)
            {
                case ManagedIdentitySourceType.AppService:
                    httpMessageHandler.ExpectedMethod = HttpMethod.Get;
                    expectedQueryParams.Add("api-version", "2019-08-01");
                    expectedQueryParams.Add("resource", resource);
                    expectedRequestHeaders.Add("X-IDENTITY-HEADER", "secret");
                    break;
                case ManagedIdentitySourceType.AzureArc:
                    httpMessageHandler.ExpectedMethod = HttpMethod.Get;
                    expectedQueryParams.Add("api-version", "2019-11-01");
                    expectedQueryParams.Add("resource", resource);
                    expectedRequestHeaders.Add("Metadata", "true");
                    break;
                case ManagedIdentitySourceType.IMDS:
                    httpMessageHandler.ExpectedMethod = HttpMethod.Get;
                    expectedQueryParams.Add("api-version", "2018-02-01");
                    expectedQueryParams.Add("resource", resource);
                    expectedRequestHeaders.Add("Metadata", "true");
                    break;
                case ManagedIdentitySourceType.CloudShell:
                    httpMessageHandler.ExpectedMethod = HttpMethod.Post;
                    expectedRequestHeaders.Add("Metadata", "true");
                    expectedRequestHeaders.Add("ContentType", "application/x-www-form-urlencoded");
                    httpMessageHandler.ExpectedPostData = new Dictionary<string, string> { { "resource", resource } };
                    break;
                case ManagedIdentitySourceType.ServiceFabric:
                    httpMessageHandler.ExpectedMethod = HttpMethod.Get;
                    expectedRequestHeaders.Add("secret", "secret");
                    expectedQueryParams.Add("api-version", "2019-07-01-preview");
                    expectedQueryParams.Add("resource", resource);
                    break;
            }

            if (managedIdentitySourceType != ManagedIdentitySourceType.CloudShell)
            {
                httpMessageHandler.ExpectedQueryParams = expectedQueryParams;
            }
            
            httpMessageHandler.ExpectedRequestHeaders = expectedRequestHeaders;

            return httpMessageHandler;
        }

        public static void AddManagedIdentityWSTrustMockHandler(
            this MockHttpManager httpManager, 
            string expectedUrl, 
            string filePath = null)
        {
            HttpResponseMessage responseMessage = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if (filePath != null)
            {
                responseMessage.Headers.Add("WWW-Authenticate", $"Basic realm={filePath}");
            }
            
            httpManager.AddMockHandler(
                    new MockHttpMessageHandler
                    {
                        ExpectedMethod = HttpMethod.Get,
                        ExpectedUrl = expectedUrl,
                        ResponseMessage = responseMessage
                    });
        }

        public static void AddRegionDiscoveryMockHandlerNotFound(
            this MockHttpManager httpManager)
        {
            httpManager.AddMockHandler(
                    new MockHttpMessageHandler
                    {
                        ExpectedMethod = HttpMethod.Get,
                        ExpectedUrl = "http://169.254.169.254/metadata/instance/compute/api-version=2020-06-01",
                        ExpectedRequestHeaders = new Dictionary<string, string>
                         {
                            {"Metadata", "true"}
                         },
                        ResponseMessage = MockHelpers.CreateFailureMessage(HttpStatusCode.NotFound, "")
                    });
        }
    }

    public enum TokenResponseType
    {
        Valid_UserFlows,
        Valid_ClientCredentials,
        Invalid_AADUnavailable503,
        /// <summary>
        /// Results in a UI Required Exception
        /// </summary>
        InvalidGrant, 

        /// <summary>
        /// Normal server exception
        /// </summary>
        InvalidClient,

        Valid_ManagedIdentityResponse
    }
 }
