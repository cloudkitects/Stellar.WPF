using Stellar.WPF.Document;

namespace Stellar.WPF.Tests.Document;

public class SegmentCollectionTest
{
    private readonly SegmentCollection<TestSegment> collection = new();
    private readonly List<TestSegment> expectedSegments = new();
    
    private readonly Random rnd = new(Environment.TickCount);

    #region helpers
    private class TestSegment : Segment
    {
        internal int ExpectedOffset, ExpectedLength;

        public TestSegment(int expectedOffset, int expectedLength)
        {
            ExpectedOffset = expectedOffset;
            ExpectedLength = expectedLength;

            StartOffset = expectedOffset;
            Length = expectedLength;
        }
    }

    private TestSegment AddSegment(int offset, int length)
    {
        var segment = new TestSegment(offset, length);
        
        collection.Add(segment);
        
        expectedSegments.Add(segment);
        
        return segment;
    }

    private void RemoveSegment(TestSegment s)
    {
        expectedSegments.Remove(s);
        
        collection.Remove(s);
    }

    private void TestRetrieval(int offset, int length)
    {
        var actual = new HashSet<TestSegment>(collection.FindOverlappingSegments(offset, length));
        var expected = new HashSet<TestSegment>();
        
        foreach (var segment in expectedSegments)
        {
            if (segment.ExpectedOffset + segment.ExpectedLength < offset)
            {
                continue;
            }

            if (segment.ExpectedOffset > offset + length)
            {
                continue;
            }

            expected.Add(segment);
        }

        Assert.True(actual.IsSubsetOf(expected));
        Assert.True(expected.IsSubsetOf(actual));
    }

    private void CheckSegments()
    {
        Assert.Equal(expectedSegments.Count, collection.Count);
        
        foreach (var segment in expectedSegments)
        {
            Assert.Equal(segment.ExpectedOffset, segment.StartOffset);
            Assert.Equal(segment.ExpectedLength, segment.Length);
        }
    }
    #endregion


    [Fact]
    public void EmptyCollectionFindingsAreNullOrEmpty()
    {
        Assert.Null(collection.FindFirstSegmentWithStartAfter(0));
        
        Assert.Empty(collection.FindSegmentsContaining(0));
        Assert.Empty(collection.FindOverlappingSegments(10, 20));
    }

    [Fact]
    public void FindsFirstSegmentWithStartAfter()
    {
        var s1 = new TestSegment(5, 10);
        var s2 = new TestSegment(10, 10);

        collection.Add(s1);
        collection.Add(s2);
        
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(-100));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(0));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(4));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(5));
        
        Assert.Equal(s2, collection.FindFirstSegmentWithStartAfter(6));
        Assert.Equal(s2, collection.FindFirstSegmentWithStartAfter(9));
        Assert.Equal(s2, collection.FindFirstSegmentWithStartAfter(10));
        
        Assert.Null(collection.FindFirstSegmentWithStartAfter(11));
        Assert.Null(collection.FindFirstSegmentWithStartAfter(100));
    }

    [Fact]
    public void FindsFirstSegmentWithStartAfterWithDuplicates()
    {
        var s1 = new TestSegment(5, 10);
        var s1b = new TestSegment(5, 7);
        var s2 = new TestSegment(10, 10);
        var s2b = new TestSegment(10, 7);

        collection.Add(s1);
        collection.Add(s1b);
        collection.Add(s2);
        collection.Add(s2b);

        Assert.Equal(s1b, collection.GetNextSegment(s1));
        
        Assert.Equal(s2b, collection.GetNextSegment(s2));
        
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(-100));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(0));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(4));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(5));
        
        Assert.Equal(s2, collection.FindFirstSegmentWithStartAfter(6));
        Assert.Equal(s2, collection.FindFirstSegmentWithStartAfter(9));
        Assert.Equal(s2, collection.FindFirstSegmentWithStartAfter(10));
        
        Assert.Null(collection.FindFirstSegmentWithStartAfter(11));
        Assert.Null(collection.FindFirstSegmentWithStartAfter(100));
    }

    [Fact]
    public void FindsFirstSegmentWithStartAfterWithDuplicates2()
    {
        var s1 = new TestSegment(5, 1);
        var s2 = new TestSegment(5, 2);
        var s3 = new TestSegment(5, 3);
        var s4 = new TestSegment(5, 4);

        collection.Add(s1);
        collection.Add(s2);
        collection.Add(s3);
        collection.Add(s4);
        
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(0));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(1));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(4));
        Assert.Equal(s1, collection.FindFirstSegmentWithStartAfter(5));
        
        Assert.Null(collection.FindFirstSegmentWithStartAfter(6));
    }

    [Fact]
    public void AddSegments()
    {
        _ = AddSegment(10, 20);
        _ = AddSegment(15, 10);

        CheckSegments();
    }

    void ChangeDocument(ChangeOffset change)
    {
        collection.UpdateOffsets(change);

        foreach (var segment in expectedSegments)
        {
            var endOffset = segment.ExpectedOffset + segment.ExpectedLength;
            
            segment.ExpectedOffset = change.ComputeOffset(segment.ExpectedOffset, AnchorMovementType.AfterInsertion);
            segment.ExpectedLength = Math.Max(0, change.ComputeOffset(endOffset, AnchorMovementType.BeforeInsertion) - segment.ExpectedOffset);
        }
    }

    [Fact]
    public void InsertsBeforeAllSegments()
    {
        _ = AddSegment(10, 20);
        _ = AddSegment(15, 10);
        
        ChangeDocument(new ChangeOffset(5, 0, 2));
        
        CheckSegments();
    }

    [Fact]
    public void ReplacesBeforeAllSegmentsTouchingFirstSegment()
    {
        _ = AddSegment(10, 20);
        _ = AddSegment(15, 10);
        
        ChangeDocument(new ChangeOffset(5, 5, 2));
        
        CheckSegments();
    }

    [Fact]
    public void InsertsAfterAllSegments()
    {
        _ = AddSegment(10, 20);
        _ = AddSegment(15, 10);
        
        ChangeDocument(new ChangeOffset(45, 0, 2));
        
        CheckSegments();
    }

    [Fact]
    public void ReplacesOverlappingWithStartOfSegment()
    {
        _ = AddSegment(10, 20);
        _ = AddSegment(15, 10);
        
        ChangeDocument(new ChangeOffset(9, 7, 2));
        
        CheckSegments();
    }

    [Fact]
    public void ReplacesWholeSegment()
    {
        _ = AddSegment(10, 20);
        _ = AddSegment(15, 10);
        
        ChangeDocument(new ChangeOffset(10, 20, 30));
        
        CheckSegments();
    }

    [Fact]
    public void ReplacesEndOfSegment()
    {
        _ = AddSegment(10, 20);
        _ = AddSegment(15, 10);
        
        ChangeDocument(new ChangeOffset(24, 6, 10));
        
        CheckSegments();
    }

    [Fact]
    public void RandomizedNoDocumentChanges()
    {
        for (var i = 0; i < 1000; i++)
        {
            switch (rnd.Next(3))
            {
                case 0:
                    AddSegment(rnd.Next(500), rnd.Next(30));
                    break;
                case 1:
                    AddSegment(rnd.Next(500), rnd.Next(300));
                    break;
                case 2:
                    if (collection.Count > 0)
                    {
                        RemoveSegment(expectedSegments[rnd.Next(collection.Count)]);
                    }
                    break;
            }

            CheckSegments();
        }
    }

    [Fact]
    public void RandomizedCloseNoDocumentChanges()
    {
        for (var i = 0; i < 1000; i++)
        {
            switch (rnd.Next(3))
            {
                case 0:
                    AddSegment(rnd.Next(20), rnd.Next(10));
                    break;
                case 1:
                    AddSegment(rnd.Next(20), rnd.Next(20));
                    break;
                case 2:
                    if (collection.Count > 0)
                    {
                        RemoveSegment(expectedSegments[rnd.Next(collection.Count)]);
                    }
                    break;
            }

            CheckSegments();
        }
    }

    [Fact]
    public void RandomizedRetrievalTest()
    {
        for (var i = 0; i < 1000; i++)
        {
            AddSegment(rnd.Next(500), rnd.Next(300));
        }

        CheckSegments();
        
        for (var i = 0; i < 1000; i++)
        {
            TestRetrieval(rnd.Next(1000) - 100, rnd.Next(500));
        }
    }

    [Fact]
    public void RandomizedWithDocumentChanges()
    {
        for (var i = 0; i < 500; i++)
        {
            switch (rnd.Next(6))
            {
                case 0:
                    AddSegment(rnd.Next(500), rnd.Next(30));
                    break;
                case 1:
                    AddSegment(rnd.Next(500), rnd.Next(300));
                    break;
                case 2:
                    if (collection.Count > 0)
                    {
                        RemoveSegment(expectedSegments[rnd.Next(collection.Count)]);
                    }
                    break;
                case 3:
                    ChangeDocument(new ChangeOffset(rnd.Next(800), rnd.Next(50), rnd.Next(50)));
                    break;
                case 4:
                    ChangeDocument(new ChangeOffset(rnd.Next(800), 0, rnd.Next(50)));
                    break;
                case 5:
                    ChangeDocument(new ChangeOffset(rnd.Next(800), rnd.Next(50), 0));
                    break;
            }
            
            CheckSegments();
        }
    }

    [Fact]
    public void RandomizedWithDocumentChangesClose()
    {
        for (var i = 0; i < 500; i++)
        {
            switch (rnd.Next(6))
            {
                case 0:
                    AddSegment(rnd.Next(50), rnd.Next(30));
                    break;
                case 1:
                    AddSegment(rnd.Next(50), rnd.Next(3));
                    break;
                case 2:
                    if (collection.Count > 0)
                    {
                        RemoveSegment(expectedSegments[rnd.Next(collection.Count)]);
                    }
                    break;
                case 3:
                    ChangeDocument(new ChangeOffset(rnd.Next(80), rnd.Next(10), rnd.Next(10)));
                    break;
                case 4:
                    ChangeDocument(new ChangeOffset(rnd.Next(80), 0, rnd.Next(10)));
                    break;
                case 5:
                    ChangeDocument(new ChangeOffset(rnd.Next(80), rnd.Next(10), 0));
                    break;
            }

            CheckSegments();
        }
    }
}
