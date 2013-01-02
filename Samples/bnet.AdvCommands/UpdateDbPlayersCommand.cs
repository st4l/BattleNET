namespace bnet.AdvCommands
{
    using bnet.BaseCommands;


    public class UpdateDbPlayersCommand : GetPlayersCommand
    {
        public override string Description
        {
            get { return "Updates online players in the database."; }
        }

        public override string Name
        {
            get { return "update_dbplayers"; }
        }
    }
}
