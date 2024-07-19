namespace Stellar.WPF.Tests.Document;

using Stellar.WPF.Document;

using Document = WPF.Document.Document;

public class AnchorTests
{
    private readonly Document document = new();
    private readonly Random rnd = new(Environment.TickCount);

    [Fact]
    public void UpdatesAnchorOffset()
    {
        var a1 = document.CreateAnchor(0);
        var a2 = document.CreateAnchor(0);

        a1.MovementType = AnchorMovementType.BeforeInsertion;
        a2.MovementType = AnchorMovementType.AfterInsertion;

        Assert.Equal(0, a1.Offset);
        Assert.Equal(0, a2.Offset);

        document.Insert(0, "x");

        Assert.Equal(0, a1.Offset);
        Assert.Equal(1, a2.Offset);
    }

    [Fact]
    public void AnchorsSurviveDeletion()
    {
        document.Text = new string(' ', 10);

        var a1 = new Anchor[11];
        var a2 = new Anchor[11];

        for (var i = 0; i < 11; i++)
        {
            a1[i] = document.CreateAnchor(i, surivesDeletion: true);
            a2[i] = document.CreateAnchor(i);
        }

        for (var i = 0; i < 11; i++)
        {
            Assert.Equal(i, a1[i].Offset);
            Assert.Equal(i, a2[i].Offset);
        }

        document.Remove(1, 8);

        for (var i = 0; i < 11; i++)
        {
            if (i <= 1)
            {
                Assert.False(a1[i].IsDeleted);
                Assert.False(a2[i].IsDeleted);
                Assert.Equal(i, a1[i].Offset);
                Assert.Equal(i, a2[i].Offset);
            }
            else if (i <= 8)
            {
                Assert.False(a1[i].IsDeleted);
                Assert.True(a2[i].IsDeleted);
                Assert.Equal(1, a1[i].Offset);
            }
            else
            {
                Assert.False(a1[i].IsDeleted);
                Assert.False(a2[i].IsDeleted);
                Assert.Equal(i - 8, a1[i].Offset);
                Assert.Equal(i - 8, a2[i].Offset);
            }
        }
    }

    [Fact]
    public void CreatesAnchors()
    {
        var anchors = new List<Anchor>();
        var expectedOffsets = new List<int>();

        document.Text = new string(' ', 1000);

        for (var i = 0; i < 1000; i++)
        {
            var offset = rnd.Next(1000);

            anchors.Add(document.CreateAnchor(offset));

            expectedOffsets.Add(offset);
        }

        for (var i = 0; i < anchors.Count; i++)
        {
            Assert.Equal(expectedOffsets[i], anchors[i].Offset);
        }

        GC.KeepAlive(anchors);
    }

    [Fact]
    public void CreatesAndCollectsAnchors()
    {
        var anchors = new List<Anchor>();
        var expectedOffsets = new List<int>();
        
        document.Text = new string(' ', 1000);
        
        for (var t = 0; t < 250; t++)
        {
            var c = rnd.Next(50);
            
            if (rnd.Next(2) == 0)
            {
                for (var i = 0; i < c; i++)
                {
                    var offset = rnd.Next(1000);
                    
                    anchors.Add(document.CreateAnchor(offset));
                    expectedOffsets.Add(offset);
                }
            }
            else if (c <= anchors.Count)
            {
                anchors.RemoveRange(0, c);
                expectedOffsets.RemoveRange(0, c);
                
                GC.Collect();
            }
            
            for (var j = 0; j < anchors.Count; j++)
            {
                Assert.Equal(expectedOffsets[j], anchors[j].Offset);
            }
        }

        GC.KeepAlive(anchors);
    }

    [Fact]
    public void MovesAnchorsDuringReplace()
    {
        document.Text = "abcd";
        
        var start = document.CreateAnchor(1);
        var middleDeletable = document.CreateAnchor(2);
        var middleSurvivorLeft = document.CreateAnchor(2);
        
        middleSurvivorLeft.SurvivesDeletion = true;
        middleSurvivorLeft.MovementType = AnchorMovementType.BeforeInsertion;
        
        var middleSurvivorRight = document.CreateAnchor(2);
        
        middleSurvivorRight.SurvivesDeletion = true;
        middleSurvivorRight.MovementType = AnchorMovementType.AfterInsertion;
        
        var end = document.CreateAnchor(3);
        
        document.Replace(1, 2, "BxC");

        Assert.Equal(1, start.Offset);
        Assert.True(middleDeletable.IsDeleted);
        Assert.Equal(1, middleSurvivorLeft.Offset);
        Assert.Equal(4, middleSurvivorRight.Offset);
        Assert.Equal(4, end.Offset);
    }

    [Fact]
    public void CreatesAndMovesAnchors()
    {
        var anchors = new List<Anchor>();
        var expectedOffsets = new List<int>();

        document.Text = new string(' ', 1000);

        for (var t = 0; t < 250; t++)
        {
            var c = rnd.Next(50);

            switch (rnd.Next(5))
            {
                case 0:
                    for (var i = 0; i < c; i++)
                    {
                        var offset = rnd.Next(document.TextLength);
                        var anchor = document.CreateAnchor(offset);

                        if (rnd.Next(2) == 0)
                        {
                            anchor.MovementType = AnchorMovementType.BeforeInsertion;
                        }
                        else
                        {
                            anchor.MovementType = AnchorMovementType.AfterInsertion;
                        }

                        anchor.SurvivesDeletion = rnd.Next(2) == 0;

                        anchors.Add(anchor);

                        expectedOffsets.Add(offset);
                    }
                    break;
                case 1:
                    if (c <= anchors.Count)
                    {
                        anchors.RemoveRange(0, c);
                        expectedOffsets.RemoveRange(0, c);

                        GC.Collect();
                    }
                    break;
                case 2:
                    var insertOffset = rnd.Next(document.TextLength);
                    var insertLength = rnd.Next(1000);

                    document.Insert(insertOffset, new string(' ', insertLength));

                    for (var i = 0; i < anchors.Count; i++)
                    {
                        if (anchors[i].MovementType == AnchorMovementType.BeforeInsertion)
                        {
                            if (expectedOffsets[i] > insertOffset)
                            {
                                expectedOffsets[i] += insertLength;
                            }
                        }
                        else
                        {
                            if (expectedOffsets[i] >= insertOffset)
                            {
                                expectedOffsets[i] += insertLength;
                            }
                        }
                    }
                    break;
                case 3:
                    var removalOffset = rnd.Next(document.TextLength);
                    var removalLength = rnd.Next(document.TextLength - removalOffset);

                    document.Remove(removalOffset, removalLength);

                    for (var i = anchors.Count - 1; i >= 0; i--)
                    {
                        if (expectedOffsets[i] > removalOffset && expectedOffsets[i] < removalOffset + removalLength)
                        {
                            if (anchors[i].SurvivesDeletion)
                            {
                                expectedOffsets[i] = removalOffset;
                            }
                            else
                            {
                                Assert.True(anchors[i].IsDeleted);
                                anchors.RemoveAt(i);
                                expectedOffsets.RemoveAt(i);
                            }
                        }
                        else if (expectedOffsets[i] > removalOffset)
                        {
                            expectedOffsets[i] -= removalLength;
                        }
                    }
                    break;
                case 4:
                    var replaceOffset = rnd.Next(document.TextLength);
                    var replaceRemovalLength = rnd.Next(document.TextLength - replaceOffset);
                    var replaceInsertLength = rnd.Next(1000);

                    document.Replace(replaceOffset, replaceRemovalLength, new string(' ', replaceInsertLength));

                    for (var i = anchors.Count - 1; i >= 0; i--)
                    {
                        if (expectedOffsets[i] > replaceOffset && expectedOffsets[i] < replaceOffset + replaceRemovalLength)
                        {
                            if (anchors[i].SurvivesDeletion)
                            {
                                if (anchors[i].MovementType == AnchorMovementType.AfterInsertion)
                                {
                                    expectedOffsets[i] = replaceOffset + replaceInsertLength;
                                }
                                else
                                {
                                    expectedOffsets[i] = replaceOffset;
                                }
                            }
                            else
                            {
                                Assert.True(anchors[i].IsDeleted);
                                anchors.RemoveAt(i);
                                expectedOffsets.RemoveAt(i);
                            }
                        }
                        else if (expectedOffsets[i] > replaceOffset)
                        {
                            expectedOffsets[i] += replaceInsertLength - replaceRemovalLength;
                        }
                        else if (expectedOffsets[i] == replaceOffset && replaceRemovalLength == 0 && anchors[i].MovementType == AnchorMovementType.AfterInsertion)
                        {
                            expectedOffsets[i] += replaceInsertLength - replaceRemovalLength;
                        }
                    }
                    break;
            }
            
            Assert.Equal(anchors.Count, expectedOffsets.Count);
            
            for (var j = 0; j < anchors.Count; j++)
            {
                Assert.Equal(expectedOffsets[j], anchors[j].Offset);
            }
        }

        GC.KeepAlive(anchors);
    }

    /// <summary>
    /// TODO: assert something
    /// </summary>
    [Fact]
    public void HandlesRepeatedDragAndDrop()
    {
        document.Text = new string(' ', 1000);
        
        for (var i = 0; i < 20; i++)
        {
            var a = document.CreateAnchor(144);
            var b = document.CreateAnchor(157);
            
            document.Insert(128, new string('a', 13));
            document.Remove(157, 13);
            
            a = document.CreateAnchor(128);
            b = document.CreateAnchor(141);

            document.Insert(157, new string('b', 13));
            document.Remove(128, 13);

            a = null;
            b = null;
            
            if ((i % 5) == 0)
            {
                GC.Collect();
            }
        }
    }

    [Fact]
    public void ReplacesSpacesWithTab()
    {
        document.Text = "a    b";

        var before = document.CreateAnchor(1);
        
        before.MovementType = AnchorMovementType.AfterInsertion;
        
        var after = document.CreateAnchor(5);
        var survivingMiddle = document.CreateAnchor(2);
        var deletedMiddle = document.CreateAnchor(3);

        document.Replace(1, 4, "\t", ChangeOffsetType.ReplaceCharacters);
        Assert.Equal("a\tb", document.Text);
        
        // the movement seems strange but it's how replacement works when text gets shorter
        Assert.Equal(1, before.Offset);
        Assert.Equal(2, after.Offset);
        Assert.Equal(2, survivingMiddle.Offset);
        Assert.Equal(2, deletedMiddle.Offset);
    }

    [Fact]
    public void ReplacesTwoCharactersWithThree()
    {
        document.Text = "a12b";
        
        var a = document.CreateAnchor(1);
        
        a.MovementType = AnchorMovementType.AfterInsertion;
        
        var b = document.CreateAnchor(3);
        
        a.MovementType = AnchorMovementType.BeforeInsertion;
        
        var m = document.CreateAnchor(2);
        
        a.MovementType = AnchorMovementType.BeforeInsertion;
        
        var n = document.CreateAnchor(2);
        
        a.MovementType = AnchorMovementType.AfterInsertion;

        document.Replace(1, 2, "123", ChangeOffsetType.ReplaceCharacters);
        
        Assert.Equal("a123b", document.Text);
        Assert.Equal(1, a.Offset);
        Assert.Equal(4, b.Offset);
        Assert.Equal(2, m.Offset);
        Assert.Equal(2, n.Offset);
    }
}
