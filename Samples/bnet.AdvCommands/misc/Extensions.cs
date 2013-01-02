using System.Data.Entity;


namespace bnet.AdvCommands.misc
{
    public static class Extensions
    {
        public static void HookSaveChanges(this DbContext dbContext, EntityFrameworkHook.SaveChangesHookHandler funcDelegate)
        {
            new EntityFrameworkHook(dbContext,funcDelegate);
        }
    }
}
