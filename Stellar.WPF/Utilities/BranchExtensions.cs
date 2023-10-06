using System.IO;

namespace Stellar.WPF.Utilities;

internal static class BranchExtensions
{
    /// <summary>
    /// Specialized T = char populate.
    /// </summary>
    internal static void Populate(this Branch<char> branch, string text, int start)
    {
        if (branch.contents is not null)
        {
            text.CopyTo(start, branch.contents, 0, branch.length);
        }
        else
        {
            branch.left!.Populate(text, start);
            branch.right!.Populate(text, start + branch.left!.length);
        }
    }

    /// <summary>
    /// Write contents to a text writer for a specialized T = char branch.
    /// </summary>
    internal static void WriteTo(this Branch<char> branch, int index, TextWriter output, int count)
    {
        if (branch.height == 0)
        {
            if (branch.contents is null)
            {
                // function
                branch.Create().WriteTo(index, output, count);
            }
            else
            {
                // leaf
                output.Write(branch.contents, index, count);
            }
        }
        else
        {
            // traverse
            if (index + count <= branch.left!.length)
            {
                branch.left.WriteTo(index, output, count);
            }
            else if (index >= branch.left.length)
            {
                branch.right!.WriteTo(index - branch.left.length, output, count);
            }
            else
            {
                var leftCount = branch.left.length - index;

                branch.left.WriteTo(index, output, leftCount);
                branch.right!.WriteTo(0, output, count - leftCount);
            }
        }
    }
}
