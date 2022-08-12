namespace SinglePass.FavIcons.Application
{
    public class FavIcon
    {
        public Guid Id { get; set; }
        public string? Host { get; set; }
        public byte[]? Bytes { get; set; }
    }
}
