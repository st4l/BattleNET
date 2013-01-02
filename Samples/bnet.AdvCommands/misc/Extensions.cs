namespace bnet.AdvCommands.misc
{
    using System.Data.Entity;


    public static class Extensions
    {
        public static void HookSaveChanges(this DbContext dbContext, 
                                           EntityFrameworkHook.SaveChangesHookHandler funcDelegate)
        {
            new EntityFrameworkHook(dbContext, funcDelegate);
        }
    }
}
