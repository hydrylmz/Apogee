using Apogee.Design;

namespace Apogee.UI.Screens.Review
{
    /// <summary>
    /// Navigation arguments passed when transitioning to the Design Review screen (ScreenId.DesignReview).
    /// Supplies the active vehicle blueprint and the current mission/facility validation context.
    /// Complies with GDD §7.4, §14.1 item 12, and §21 M2-05.
    /// </summary>
    public sealed class ReviewNavigationArgs
    {
        public Blueprint Blueprint { get; }
        public ValidationContext Context { get; }

        public ReviewNavigationArgs(Blueprint blueprint, ValidationContext context = null)
        {
            Blueprint = blueprint;
            Context = context;
        }
    }
}
