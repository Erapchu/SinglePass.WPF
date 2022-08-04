using System;

namespace PasswordManager.Application
{
    public class FavIcon
    {
        public Guid Id { get; set; }
        public string Host { get; set; }
        public byte[] Bytes { get; set; }
    }
}
