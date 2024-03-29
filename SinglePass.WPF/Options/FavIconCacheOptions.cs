﻿using System;

namespace SinglePass.WPF.Options
{
    public class FavIconCacheOptions
    {
        private string _connectionString;
        public string ConnectionString
        {
            get
            {
                return _connectionString;
            }
            init
            {
                var expanded = Environment.ExpandEnvironmentVariables(value);
                _connectionString = expanded.Replace('\\', '/');
            }
        }
    }
}
