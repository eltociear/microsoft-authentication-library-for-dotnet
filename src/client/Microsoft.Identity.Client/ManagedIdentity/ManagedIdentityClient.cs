﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Identity.Client.Extensibility;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Internal;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Identity.Client.ManagedIdentity
{
    /// <summary>
    /// Class to initialize a managed identity and identify the service.
    /// </summary>
    internal class ManagedIdentityClient
    {
        internal const string MsiUnavailableError =
            "ManagedIdentityCredential authentication unavailable. No Managed Identity endpoint found.";

        private Lazy<ManagedIdentitySource> _identitySource;
        RequestContext _context;

        protected ManagedIdentityClient()
        {
        }

        public ManagedIdentityClient(RequestContext requestContext)
        {
            _context = requestContext;
            _identitySource = new Lazy<ManagedIdentitySource>(() => SelectManagedIdentitySource(requestContext));
        }

        public virtual async Task<ManagedIdentityResponse> AuthenticateCoreAsync(AppTokenProviderParameters parameters,
            CancellationToken cancellationToken)
        {
            return await _identitySource.Value.AuthenticateAsync(parameters, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<AppTokenProviderResult> AppTokenProviderImplAsync(AppTokenProviderParameters parameters)
        {
            ManagedIdentityResponse response = await AuthenticateCoreAsync(parameters, parameters.CancellationToken).ConfigureAwait(false);

            return new AppTokenProviderResult() { AccessToken = response.AccessToken, ExpiresInSeconds = DateTimeHelpers.GetDurationFromNowInSeconds(response.ExpiresOn) };
        }

        // This method tries to create managed identity source for different sources, if none is created then defaults to imds.
        private static ManagedIdentitySource SelectManagedIdentitySource(RequestContext requestContext)
        {
            return AppServiceManagedIdentitySource.TryCreate(requestContext) ??
                new ImdsManagedIdentitySource(requestContext);
        }


    }
}
