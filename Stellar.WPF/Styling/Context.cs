using System;
using System.Collections.Generic;

using Stellar.WPF.Utilities;

namespace Stellar.WPF.Styling;

/// <summary>
/// A context holds a collection of spans and rules. 
/// </summary>
[Serializable]
public class Context
{
    /// <summary>
    /// Gets the list of spans.
    /// </summary>
    public IList<Span> Spans { get; private set; }

    /// <summary>
    /// Gets the list of rules.
    /// </summary>
    public IList<Rule> Rules { get; private set; }

    /// <summary>
    /// Gets/Sets the name of the rule set.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Creates a new RuleSet instance.
    /// </summary>
    public Context()
    {
        Spans = new NullSafeCollection<Span>();
        Rules = new NullSafeCollection<Rule>();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"[{GetType().Name} {Name}]";
    }
}

