namespace SimpleStore
{
    [BinarySerializerGenerator.GenerateBinarySerializer]
    public partial class UserProfile
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public void SerializeToBinary(System.IO.Stream stream)
        {
            using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Id);
                writer.Write(Username ?? string.Empty);
                writer.Write(CreatedAt.Ticks);
            }
        }

        public static UserProfile DeserializeFromBinary(System.IO.Stream stream)
        {
            using (var reader = new System.IO.BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var profile = new UserProfile
                {
                    Id = reader.ReadInt32(),
                    Username = reader.ReadString(),
                    CreatedAt = new DateTime(reader.ReadInt64())
                };
                return profile;
            }
        }
    }
}