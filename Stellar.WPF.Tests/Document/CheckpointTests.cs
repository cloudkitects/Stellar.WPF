namespace Stellar.WPF.Tests.Document;

using Document = WPF.Document.Document;

public class CheckpointTests
{
    [Fact]
    public void NoChanges()
    {
        var document = new Document("initial text");
        
        var snapshot1 = document.CreateSnapshot();
        var snapshot2 = document.CreateSnapshot();

        Assert.Equal(snapshot1.ToString(), snapshot2.ToString());
        
        Assert.Equal(0, snapshot2.Checkpoint!.GetDistanceTo(snapshot1.Checkpoint!));
        Assert.Empty(snapshot2.Checkpoint!.GetChangesUpTo(snapshot1.Checkpoint!));
        Assert.Equal(document.Text, snapshot1.Text);
        Assert.Equal(document.Text, snapshot2.Text);
    }

    [Fact]
    public void ForwardChanges()
    {
        var document = new Document("initial text");

        var snapshot1 = document.CreateSnapshot();

        document.Replace(0, 7, "nw");
        document.Insert(1, "e");

        var snapshot2 = document.CreateSnapshot();

        Assert.Equal(-1, snapshot1.Checkpoint!.GetDistanceTo(snapshot2.Checkpoint!));
        
        var changes = snapshot1.Checkpoint!.GetChangesUpTo(snapshot2.Checkpoint!)
            .Select(c => c.InsertedText.Text)
            .ToArray();

        Assert.Equal(2, changes.Length);
        Assert.Equal("nw", changes[0]);
        Assert.Equal("e",  changes[1]);
        Assert.Equal("initial text", snapshot1.Text);
        Assert.Equal("new text", snapshot2.Text);
    }

    [Fact]
    public void BackwardChanges()
    {
        var document = new Document("initial text");

        var snapshot1 = document.CreateSnapshot();

        document.Replace(0, 7, "nw");
        document.Insert(1, "e");

        var snapshot2 = document.CreateSnapshot();

        Assert.Equal(1, snapshot2.Checkpoint!.GetDistanceTo(snapshot1.Checkpoint!));

        var changes = snapshot2.Checkpoint!.GetChangesUpTo(snapshot1.Checkpoint!)
            .Select(c => c.InsertedText.Text)
            .ToArray();

        Assert.Equal(2, changes.Length);
        Assert.Equal("", changes[0]);
        Assert.Equal("initial", changes[1]);
        Assert.Equal("initial text", snapshot1.Text);
        Assert.Equal("new text", snapshot2.Text);
    }
}
