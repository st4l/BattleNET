namespace bnet.BaseCommands
{
    public class PlayerInfo
    {
        // 20:43:35 - [#] [IP Address]:[Port] [Ping] [GUID] [Name]
        // 20:43:35 - 0   24.9.36.119:2304      47   9554448a74a0ec6bd5ef39f54b6e28ba(OK) nino
        public int Id { get; set; }
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public int Ping { get; set; }
        public string Guid { get; set; }
        public bool Verified { get; set; }
        public bool InLobby { get; set; }
    }
}
