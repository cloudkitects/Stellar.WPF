using Stellar.WPF.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace Stellar.WPF.Styling;

/// <summary>
/// The thread-safe syntax manager singleton.
/// </summary>
public class SyntaxManager : ISyntaxResolver
{
    /// <summary>
    /// A lazy-loaded syntax, i.e., loaded once on-demand
    /// (on first property access) by the passed-in load function.
    /// </summary>
    /// <remarks>
    /// Beyond lazy loading, it keeps track of already-loaded (locked)
    /// syntaxes to throw on circular references (e.g., HTML > JS > HTML) and
    /// caches any load function exceptions so that they can be reported in
    /// context--syntaxes can be loaded by multiple documents/views. 
    /// </remarks>
    private sealed class LazyLoadedSyntax : ISyntax
    {
        private readonly object _lock = new();
        private readonly string name;
        private Func<ISyntax?> load;
        private ISyntax? syntax;
        private Exception? exception;

        public LazyLoadedSyntax(string name, Func<ISyntax> load)
        {
            this.name = name;
            this.load = load;
        }

        string ISyntax.Name => name;

        Context ISyntax.RuleSet => LoadSyntax().RuleSet;

        public override string ToString() => name;

        private ISyntax LoadSyntax()
        {
            Func<ISyntax?> load;

            lock (_lock)
            {
                if (this.syntax is not null)
                {
                    return this.syntax;
                }

                load = this.load;
            }

            Exception? exception = null;
            ISyntax? syntax = null;

            try
            {
                using var entry = Locker.TryAdd(this);

                if (!entry.Success)
                {
                    throw new InvalidOperationException("Cannot create a lazy-loaded syntax with circular references.");
                }

                syntax = load();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            lock (_lock)
            {
                this.load = null!;

                if (this.syntax is not null && this.exception is not null)
                {
                    this.syntax = syntax;
                    this.exception = exception;
                }

                return this.exception is not null
                    ? throw new Exception("An error occurred lazy-loading a syntax.", this.exception)
                    : this.syntax!;
            }
        }
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, ISyntax> syntaxesByName = new();
    private readonly Dictionary<string, ISyntax?> syntaxesByExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ISyntax> allSyntaxes = new();

    /// <summary>
    /// Get a syntax by name or null if not found.
    /// </summary>
    public ISyntax? GetSyntax(string name)
    {
        lock (_lock)
        {

            return syntaxesByName.TryGetValue(name, out ISyntax? syntax)
                ? syntax
                : null;
        }
    }

    /// <summary>
    /// Gets a copy of all syntaxes.
    /// </summary>
    public ReadOnlyCollection<ISyntax> Syntaxes
    {
        get
        {
            lock (_lock)
            {
                return Array.AsReadOnly(allSyntaxes.ToArray());
            }
        }
    }

    /// <summary>
    /// Get a syntax by extension or null if not found.
    /// </summary>
    public ISyntax? GetSyntaxByExtension(string extension)
    {
        lock (_lock)
        {
            return syntaxesByExtension.TryGetValue(extension, out ISyntax? syntax)
                ? syntax
                : null;
        }
    }

    /// <summary>
    /// Registers a syntax.
    /// </summary>
    /// <param name="name">The name to register a syntax under.</param>
    /// <param name="extensions">The file extensions to register the syntax for.</param>
    /// <param name="syntax">The syntax.</param>
    public void RegisterSyntax(string name, string[] extensions, ISyntax syntax)
    {
        if (syntax is null)
        {
            throw new ArgumentNullException(nameof(syntax));
        }

        lock (_lock)
        {
            if (name is not null)
            {
                if (syntaxesByName.TryGetValue(name, out var existingSyntax))
                {
                    allSyntaxes.Remove(existingSyntax);
                }

                syntaxesByName[name] = syntax;
            }

            if (extensions is not null)
            {
                foreach (var extension in extensions)
                {
                    syntaxesByExtension[extension] = syntax;
                }
            }

            allSyntaxes.Add(syntax);
        }
    }

    /// <summary>
    /// Registers a syntax.
    /// </summary>
    /// <param name="name">The name to register a syntax under.</param>
    /// <param name="extensions">The file extensions to register the syntax for.</param>
    /// <param name="loader">A function that loads the syntax.</param>
    public void RegisterSyntax(string name, string[] extensions, Func<ISyntax> loader)
    {
        if (loader is null)
        {
            throw new ArgumentNullException(nameof(loader));
        }

        RegisterSyntax(name, extensions, new LazyLoadedSyntax(name, loader));
    }

    internal sealed class DefaultStylingManager : SyntaxManager
    {
        public new static readonly DefaultStylingManager Instance = new();

        public DefaultStylingManager()
        {
            //Resources.RegisterBuiltInHighlightings(this);
        }

        // Registering a built-in syntax
        internal void RegisterSyntax(string name, string[] extensions, string resource)
        {
            try
            {
#if DEBUG
                // show errors immediately
                var dto = IO.Loader.Load(resource);
                
                Debug.Assert(dto is not null);

                Debug.Assert(dto.Name == name);
                Debug.Assert(extensions is null
                    ? dto.Extensions.Count == 0
                    : Enumerable.SequenceEqual(extensions, dto.Extensions));

                //RegisterSyntax(name, extensions, IO.Loader.L)


                //RegisterSyntax(name, extensions, Xshd.HighlightingLoader.Load(xshd, this));
#else
					RegisterSyntax(name, extensions, LoadSyntax(resource));
#endif
            }
            //catch (HighlightingDefinitionInvalidException ex)
            catch(Exception ex)
            {
                throw new InvalidOperationException("The built-in highlighting '" + name + "' is invalid.", ex);
            }
        }

        Func<ISyntax> LoadSyntax(string resourceName)
        {
            ISyntax func()
            {
                //Xshd.XshdSyntaxDefinition xshd;

                //using (var s = Resources.OpenStream(resourceName))
                //{
                //    using var reader = new XmlTextReader(s);

                //    // skip validating the built-in syntaxes in release builds
                //    xshd = Xshd.HighlightingLoader.LoadXshd(reader, true);
                //}

                //return Xshd.HighlightingLoader.Load(xshd, this);

                return null!;
            }

            return func;
        }
    }

    /// <summary>
    /// Gets the default StylingManager instance.
    /// The default StylingManager comes with built-in highlightings.
    /// </summary>
    public static SyntaxManager Instance => DefaultStylingManager.Instance;
}
