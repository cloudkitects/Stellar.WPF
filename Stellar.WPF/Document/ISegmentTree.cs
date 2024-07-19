namespace Stellar.WPF.Document;

/// <summary>
/// Interface for segments to access the segment collection (direct reference do not work on generics).
/// </summary>
interface ISegmentTree
{
    void Add(Segment s);
    void Remove(Segment s);
    void UpdateAugmentedData(Segment s);
}
