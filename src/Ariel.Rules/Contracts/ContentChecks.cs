namespace Ariel.Game;

/// <summary>A load-time check of an invariant that spans several mods' content, which a mod cannot run while it
/// registers because the content it reads about may not be registered yet: whether the units a feature gives a job can
/// be trained at all, say. The base game runs every registered check once content freezes, after every mod registered,
/// and fails the load with all of their problems together. Checks read the frozen content only.</summary>
public interface IContentCheck
{
    /// <summary>What is wrong with the loaded content, one line each; empty when nothing is.</summary>
    IEnumerable<string> Problems(Content.ContentDb content);
}
