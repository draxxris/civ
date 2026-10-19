using Civ.Content;

namespace Civ.Game;

/// <summary>Optional selection rules. Refresh runs only at authoritative mutation boundaries.</summary>
public interface IResearchSelectionPolicy
{
    string? ValidateQueue(Simulation sim, int player, IReadOnlyList<string> queue);
    void Refresh(Simulation sim, int team);
}

public enum ResearchChoiceLayout { List, Cards }
public sealed record ResearchSelectionView(string Title, ResearchChoiceLayout Layout, IReadOnlyList<string> Offered);
public interface IResearchSelectionPresentation
{
    ResearchSelectionView? Describe(Simulation sim, int player);
}

/// <summary>Research completion, distinct from acquisition through trade or a reward.</summary>
public sealed record ResearchCompleted(int Team, string Tech) : DomainEvent;

/// <summary>A complete, browsable tree. Requirements retain their nested logical groups.</summary>
public sealed record TechTreeNode(string Id, string Name, string Era, string Help, int Column, int Row,
    Requirement? Requirements, bool Known, bool Repeatable, bool Researchable, bool Selectable, bool Current,
    long Cost, long Progress, IReadOnlyList<string> Failures);
public sealed record TechTreeView(IReadOnlyList<TechTreeNode> Nodes);
public interface ITechTreePresentation
{
    TechTreeView? Describe(Simulation sim, int player);
}
