﻿using Newtonsoft.Json;
using NLog;
using PasswordManager.Helpers;
using PasswordManager.Helpers.Threading;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordManager.Services
{
    public class SettingsService
    {
        public Lazy<JsonSerializerSettings> _lazyDefaultSerializerSettings = new(() => new JsonSerializerSettings()
        {
            DefaultValueHandling = DefaultValueHandling.Include
        });
        public JsonSerializerSettings DefaultSerializerSettings => _lazyDefaultSerializerSettings.Value;

        private readonly ILogger _logger;
        private List<Credential> _credentialsList;

        public SettingsService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<List<Credential>> LoadCredentialsFromFileAsync()
        {
            if (_credentialsList is null)
            {
                _credentialsList = await Task.Run(() =>
                {
                    var credentials = new List<Credential>();
                    try
                    {
                        var pathToPasswordsFile = Constants.PathToPasswordsFile;
                        if (!File.Exists(pathToPasswordsFile))
                        {
                            // File is not exists yet
                            return credentials;
                        }

                        var passPathBytes = Encoding.UTF8.GetBytes(Constants.PathToPasswordsFile);
                        var hashData = System.Security.Cryptography.SHA256.HashData(passPathBytes);
                        var hashedPath = Encoding.UTF8.GetString(hashData);

                        using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, hashedPath);
                        using var fileStream = File.Open(Constants.PathToPasswordsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var streamReader = new StreamReader(fileStream);
                        using var jsonReader = new JsonTextReader(streamReader);
                        var serializer = JsonSerializer.Create(DefaultSerializerSettings);

                        try
                        {
                            credentials = serializer.Deserialize<List<Credential>>(jsonReader);
                        }
                        catch (JsonException jsex)
                        {
                            _logger.Warn("Failed to deserialize credentials settings file content due to exception: {0}", jsex);

                            if (credentials is null)
                            {
                                credentials = new();
                                _logger.Info("Using default settings");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                    }

                    return credentials;
                });
            }

            return _credentialsList;
        }

        public async Task SaveCredentialsToFileAsync()
        {
            if (_credentialsList is null)
                return;

            await Task.Run(() =>
            {
                var passwordsPath = Constants.PathToPasswordsFile;
                using var waitHandleLocker = EventWaitHandleLocker.MakeWithEventHandle(true, EventResetMode.AutoReset, passwordsPath);
                using var fileStream = File.Open(passwordsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var streamWriter = new StreamWriter(fileStream);
                using var jsonWriter = new JsonTextWriter(streamWriter);
                var serializer = JsonSerializer.Create(DefaultSerializerSettings);

                try
                {
                    serializer.Serialize(jsonWriter, _credentialsList);
                }
                catch (JsonException jsex)
                {
                    _logger.Warn("Failed to serialize credentials settings to file due to exception: {0}", jsex);
                }
            });
        }
    }
}