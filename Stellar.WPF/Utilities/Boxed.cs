namespace Stellar.WPF.Utilities;

/// <summary>
/// A neat trick for reusing boxed booleans.
/// </summary>
static class Boxed
{
    public static readonly object True = true;
    public static readonly object False = false;

    public static object Box(bool value) => value ? True : False;
}