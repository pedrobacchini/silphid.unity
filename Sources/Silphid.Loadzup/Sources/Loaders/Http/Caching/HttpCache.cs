﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using log4net;
using Silphid.Extensions;
using UniRx;
using UnityEngine;

namespace Silphid.Loadzup.Http.Caching
{
    /// <summary>
    /// Manages local persistence for HTTP files.
    /// </summary>
    public class HttpCache : IHttpCache
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HttpCache));
        private const string HeadersExtension = ".Headers";
        private readonly TimeSpan _defaultExpirySpan;
        private string _cacheDir;

        public HttpCache(TimeSpan defaultExpirySpan)
        {
            _defaultExpirySpan = defaultExpirySpan;
        }

        public void Clear()
        {
            Directory
                .GetFiles(GetCacheDir())
                .ForEach(File.Delete);
        }

        public void ClearExpired()
        {
            int deletedCount = 0;
            var utcNow = DateTime.UtcNow;
            
            Directory
                .GetFiles(GetCacheDir())
                .Where(file => !file.EndsWith(HeadersExtension))
                .ForEach(mainFile =>
                {
                    // Find corresponding header
                    var headersFile = mainFile + HeadersExtension;
                    if (!File.Exists(headersFile))
                    {
                        // Main file without headers file is useless: delete it
                        File.Delete(mainFile);
                        deletedCount++;
                        return;
                    }

                    // Load headers and check if expired
                    var fileDate = File.GetLastWriteTimeUtc(mainFile);
                    var headers = LoadHeaders(headersFile);
                    if (IsExpired(headers, utcNow, fileDate))
                    {
                        File.Delete(mainFile);
                        File.Delete(headersFile);
                        deletedCount++;
                    }
                });
            
            Log.Debug($"Deleted {deletedCount} expired files.");
        }

        public bool Contains(Uri uri) => File.Exists(GetFilePath(uri));

        public IObservable<byte[]> Load(Uri uri) =>
            Observable
                .Start(() => File.ReadAllBytes(GetFilePath(uri)))
                .ObserveOnMainThread();

        public Dictionary<string, string> LoadHeaders(Uri uri) =>
            LoadHeaders(GetHeadersFile(GetFilePath(uri)));

        private Dictionary<string, string> LoadHeaders(string headersFile)
        {
            if (!File.Exists(headersFile))
                return null;

            return (from line in File.ReadAllLines(headersFile)
                    let separatorIndex = GetSeparatorIndex(line)
                    select new
                    {
                        Key = line.Left(separatorIndex).Trim(),
                        Value = line.Substring(separatorIndex + 2).Trim()
                    })
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsExpired(Dictionary<string, string> headers, DateTime utcNow, DateTime fileDate)
        {
            var cacheControl = new CacheControl(headers, fileDate, _defaultExpirySpan);
            return utcNow >= cacheControl.Expiry;
        }

        public void Save(Uri uri, byte[] bytes, IDictionary<string, string> headers)
        {
            var filePath = GetFilePath(uri);
            Log.Debug($"Save cache to: {filePath}");
            
            if (!headers.ContainsKey(KnownHttpHeaders.ContentType))
                Log.Warn($"Missing Content-Type in headers of {uri}");
            
            File.WriteAllBytes(filePath, bytes);
            File.WriteAllLines(GetHeadersFile(filePath), headers.Select(x => $"{x.Key}: {x.Value}").ToArray());
        }

        private string GetFilePath(Uri uri) =>
            GetCacheDir() + Path.DirectorySeparatorChar + GetEscapedFileName(uri);

        private string GetHeadersFile(string filePath) =>
            filePath + HeadersExtension;

        private int GetSeparatorIndex(string line)
        {
            var index = line.IndexOf(": ", StringComparison.Ordinal);
            if (index == -1)
                throw new InvalidOperationException("Malformed cache headers file");
            return index;
        }

        private string GetCacheDir()
        {
            if (_cacheDir != null)
                return _cacheDir;
            
            _cacheDir = Application.temporaryCachePath +
                      Path.DirectorySeparatorChar +
                      Application.version +
                      Path.DirectorySeparatorChar +
                      "Silphid.Loadzup.Cache";

            Log.Debug($"Cache path: {_cacheDir}");
            Directory.CreateDirectory(_cacheDir);
            return _cacheDir;
        }

        private string GetEscapedFileName(Uri uri)
        {
            var invalidCharacters = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + ":");
            var regex = new Regex($"[{invalidCharacters}]");
            return regex.Replace(uri.AbsoluteUri, "_");
        }
    }
}