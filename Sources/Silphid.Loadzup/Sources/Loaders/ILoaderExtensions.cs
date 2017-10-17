﻿using System;
using System.Collections.Generic;
using Silphid.Loadzup.Caching;
using Silphid.Loadzup.Http;
using UnityEngine;

namespace Silphid.Loadzup
{
    public static class ILoaderExtensions
    {
        public static IObservable<T> Load<T>(this ILoader This, string uri, Options options = null) =>
            This.Load<T>(new Uri(uri), options);
        
        public static ILoader With(this ILoader This, CachePolicy? cachePolicy) =>
            new CachePolicyLoaderDecorator(This, cachePolicy);
        
        public static ILoader With(this ILoader This, ContentType contentType) =>
            new ContentTypeLoaderDecorator(This, contentType);
        
        public static ILoader WithHeader(this ILoader This, string key, string value) =>
            new HeaderLoaderDecorator(This, key, value);
        
        public static ILoader WithHeaders(this ILoader This, Dictionary<string, string> headers) =>
            new HeadersLoaderDecorator(This, headers);
        
        public static ILoader With(this ILoader This, WWWForm form) =>
            new PostLoaderDecorator(This, form);
        
        public static ILoader WithEmptyForm(this ILoader This) =>
            new PostLoaderDecorator(This, new WWWForm());
        
        public static ILoader WithField(this ILoader This, string key, string value) =>
            new PostFieldLoaderDecorator(This, key, value);
        
        public static ILoader WithField(this ILoader This, string key, int value) =>
            new PostFieldLoaderDecorator(This, key, value.ToString());
        
        public static ILoader WithBody(this ILoader This, string body) =>
            new PutLoaderDecorator(This, body);

        public static ILoader WithUrlEncodedBody(this ILoader This, string body) =>
            This
                .WithBody(body)
                .WithHeader(KnownHttpHeaders.ContentType, KnownMediaType.ApplicationWWWFormUrlEncoded);
    }
}