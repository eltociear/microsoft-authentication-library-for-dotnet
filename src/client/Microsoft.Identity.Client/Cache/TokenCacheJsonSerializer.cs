﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Identity.Client.Cache.Items;
using Microsoft.Identity.Client.Utils;
#if SUPPORTS_SYSTEM_TEXT_JSON
using JToken = System.Text.Json.Nodes.JsonNode;
#else
using Microsoft.Identity.Json.Linq;
#endif

namespace Microsoft.Identity.Client.Cache
{
    internal class TokenCacheJsonSerializer : ITokenCacheSerializable
    {
        private readonly ITokenCacheAccessor _accessor;

        public TokenCacheJsonSerializer(ITokenCacheAccessor accessor)
        {
            _accessor = accessor;
        }

        public byte[] Serialize(IDictionary<string, JToken> unknownNodes)
        {
            return Serialize(null, unknownNodes);
        }

        public byte[] Serialize(string cacheKey, IDictionary<string, JToken> unknownNodes)
        {
            var cache = new CacheSerializationContract(unknownNodes);
            foreach (var token in _accessor.GetAllAccessTokens(cacheKey))
            {
                cache.AccessTokens[token.CacheKey] = token;
            }

            foreach (var token in _accessor.GetAllRefreshTokens(cacheKey))
            {
                cache.RefreshTokens[token.CacheKey] = token;
            }

            foreach (var token in _accessor.GetAllIdTokens(cacheKey))
            {
                cache.IdTokens[token.CacheKey] = token;
            }

            foreach (var accountItem in _accessor.GetAllAccounts(cacheKey))
            {
                cache.Accounts[accountItem.CacheKey] = accountItem;
            }

            foreach (var appMetadata in _accessor.GetAllAppMetadata())
            {
                cache.AppMetadata[appMetadata.CacheKey] = appMetadata;
            }

            return cache.ToJsonString()
                        .ToByteArray();
        }

        public IDictionary<string, JToken> Deserialize(byte[] bytes, bool clearExistingCacheData)
        {
            CacheSerializationContract cache;

            try
            {
                cache = CacheSerializationContract.FromJsonString(CoreHelpers.ByteArrayToString(bytes));
            }
            catch (Exception ex)
            {
                throw new MsalClientException(MsalError.JsonParseError, MsalErrorMessage.TokenCacheJsonSerializerFailedParse, ex);
            }

            if (clearExistingCacheData)
            {
                _accessor.Clear();
            }

            if (cache.AccessTokens != null)
            {
                foreach (var atItem in cache.AccessTokens.Values)
                {
                    _accessor.SaveAccessToken(atItem);
                }
            }

            if (cache.RefreshTokens != null)
            {
                foreach (var rtItem in cache.RefreshTokens.Values)
                {
                    _accessor.SaveRefreshToken(rtItem);
                }
            }

            if (cache.IdTokens != null)
            {
                foreach (var idItem in cache.IdTokens.Values)
                {
                    _accessor.SaveIdToken(idItem);
                }
            }

            if (cache.Accounts != null)
            {
                foreach (var account in cache.Accounts.Values)
                {
                    _accessor.SaveAccount(account);
                }
            }

            if (cache.AppMetadata != null)
            {
                foreach (var appMetadata in cache.AppMetadata.Values)
                {
                    _accessor.SaveAppMetadata(appMetadata);
                }
            }

            return cache.UnknownNodes;
        }
    }
}
