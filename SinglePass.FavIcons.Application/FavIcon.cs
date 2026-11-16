namespace SinglePass.FavIcons.Application
{
    public enum FavIconStatus
    {
        Missing = 0,
        Available = 1,
        TemporaryError = 2,
    }

    public class FavIcon
    {
        public Guid Id { get; set; }
        public string? Host { get; set; }
        public int Size { get; set; }
        public byte[]? Bytes { get; set; }
        public DateTime? LastCheckedUtc { get; set; }
        public DateTime? LastUpdatedUtc { get; set; }
        public FavIconStatus Status { get; set; }
    }
}
