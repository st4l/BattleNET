using System.Data.Entity;

namespace EntityFramework.Hooking
{
    public static class Extensions
    {
        public static void HookSaveChanges(this DbContext dbContext, EntityFrameworkHook.SaveChangesHookHandler funcDelegate)
        {
            new EntityFrameworkHook(dbContext,funcDelegate);
        }
    }
}
