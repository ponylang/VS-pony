using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;


namespace Pony
{
  [Export(typeof(IViewTaggerProvider))]
  [ContentType("Pony")]
  [TagType(typeof(TextMarkerTag))]
  public class BracketMatchTaggerProvider : IViewTaggerProvider
  {
    [Import]
    internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
      if(textView == null)
        return null;

      // Only highlight on the top-level buffer 
      if(textView.TextBuffer != buffer)
        return null;

      ITagAggregator<LexTag> lexTagAggregator = aggregatorFactory.CreateTagAggregator<LexTag>(buffer);
      return new BracketMatchTagger(textView, buffer, lexTagAggregator) as ITagger<T>;
    }
  }


  public enum BracketSide
  {
    Start, Intermediate, End
  }


  public enum BracketGroup
  {
    Paren, Square, Block
  }


  public class BracketMatchTagger : ITagger<TextMarkerTag>, IDisposable
  {
    private readonly ITextView _view;
    private readonly ITextBuffer _buffer;
    private readonly ITagAggregator<LexTag> _aggregator;
    private readonly Dictionary<TokenId, Tuple<BracketSide, BracketGroup>> _bracketTokens;
    private readonly object _updateLock = new object();

    public BracketMatchTagger(ITextView view, ITextBuffer buffer, ITagAggregator<LexTag> lexTagAggregator)
    {
      _view = view;
      _buffer = buffer;
      _aggregator = lexTagAggregator;

      _bracketTokens = new Dictionary<TokenId, Tuple<BracketSide, BracketGroup>>();
      _bracketTokens[TokenId.LParen] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Paren);
      _bracketTokens[TokenId.RParen] = new Tuple<BracketSide, BracketGroup>(BracketSide.End, BracketGroup.Paren);
      _bracketTokens[TokenId.LSquare] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Square);
      _bracketTokens[TokenId.RSquare] = new Tuple<BracketSide, BracketGroup>(BracketSide.End, BracketGroup.Square);
      _bracketTokens[TokenId.If] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Block);
      _bracketTokens[TokenId.While] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Block);
      _bracketTokens[TokenId.For] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Block);
      _bracketTokens[TokenId.Repeat] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Block);
      _bracketTokens[TokenId.Try] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Block);
      _bracketTokens[TokenId.Match] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Block);
      _bracketTokens[TokenId.With] = new Tuple<BracketSide, BracketGroup>(BracketSide.Start, BracketGroup.Block);
      _bracketTokens[TokenId.Then] = new Tuple<BracketSide, BracketGroup>(BracketSide.Intermediate, BracketGroup.Block);
      _bracketTokens[TokenId.Else] = new Tuple<BracketSide, BracketGroup>(BracketSide.Intermediate, BracketGroup.Block);
      _bracketTokens[TokenId.ElseIf] = new Tuple<BracketSide, BracketGroup>(BracketSide.Intermediate, BracketGroup.Block);
      _bracketTokens[TokenId.In] = new Tuple<BracketSide, BracketGroup>(BracketSide.Intermediate, BracketGroup.Block);
      _bracketTokens[TokenId.Do] = new Tuple<BracketSide, BracketGroup>(BracketSide.Intermediate, BracketGroup.Block);
      _bracketTokens[TokenId.Until] = new Tuple<BracketSide, BracketGroup>(BracketSide.Intermediate, BracketGroup.Block);
      _bracketTokens[TokenId.End] = new Tuple<BracketSide, BracketGroup>(BracketSide.End, BracketGroup.Block);

      _aggregator.TagsChanged += LexTagChanged;
      _view.Caret.PositionChanged += CaretPositionChanged;
    }

    public void Dispose()
    {
      _view.Caret.PositionChanged -= CaretPositionChanged;
      _aggregator.TagsChanged -= LexTagChanged;
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
    {
      Update();
    }

    private void LexTagChanged(object sender, TagsChangedEventArgs e)
    {
      Update();
    }

    private void Update()
    {
      var tempEvent = TagsChanged;

      if(tempEvent != null)
        tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0,
            _buffer.CurrentSnapshot.Length)));
    }

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      SnapshotPoint caretPosition = _view.Caret.Position.BufferPosition;

      if(caretPosition >= _buffer.CurrentSnapshot.Length)
        yield break;

      TokenId tagType = TokenId.Ignore;
      SnapshotPoint tokenPosition = new SnapshotPoint();
      var caretSpan = new SnapshotSpan(caretPosition, 1);

      foreach(var tagSpan in _aggregator.GetTags(caretSpan))
      {
        tagType = tagSpan.Tag.type;
        tokenPosition = tagSpan.Span.GetSpans(caretSpan.Snapshot)[0].Start;
      }

      if(!_bracketTokens.ContainsKey(tagType))
        yield break;

      Tuple<BracketSide, BracketGroup> bracket = _bracketTokens[tagType];

      if(bracket.Item1 != BracketSide.Start)
        foreach(var tag in MatchBackward(bracket.Item2, tokenPosition))
          yield return tag;

      foreach(var tag in MatchForward(bracket.Item1, bracket.Item2, tokenPosition))
        yield return tag;
    }

    private IEnumerable<ITagSpan<TextMarkerTag>> MatchForward(BracketSide side, BracketGroup group, SnapshotPoint startPoint)
    {
      ITextSnapshotLine line = startPoint.GetContainingLine();
      int lineOffset = startPoint.Position - line.Start.Position;
      int nest = (side == BracketSide.Start) ? -1 : 0;

      for(int lineNumber = line.LineNumber; lineNumber < _buffer.CurrentSnapshot.LineCount; lineNumber++)
      {
        line = line.Snapshot.GetLineFromLineNumber(lineNumber);
        var span = new SnapshotSpan(line.Start + lineOffset, line.Length - lineOffset);

        foreach(var tagSpan in _aggregator.GetTags(span))
        {
          var tagType = tagSpan.Tag.type;

          if(_bracketTokens.ContainsKey(tagType))
          {
            Tuple<BracketSide, BracketGroup> bracket = _bracketTokens[tagType];

            if(bracket.Item2 == group)
            {
              switch(bracket.Item1)
              {
                case BracketSide.Start:
                  if(nest < 0)
                    yield return Mark(tagSpan, span);

                  nest++;
                  break;

                case BracketSide.Intermediate:
                  if(nest == 0)
                    yield return Mark(tagSpan, span);

                  break;

                case BracketSide.End:
                  if(nest == 0)
                  {
                    yield return Mark(tagSpan, span);
                    yield break;
                  }

                  nest--;
                  break;
              }
            }
          }
        }

        lineOffset = 0;
      }
    }

    private IEnumerable<ITagSpan<TextMarkerTag>> MatchBackward(BracketGroup group, SnapshotPoint startPoint)
    {
      ITextSnapshotLine line = startPoint.GetContainingLine();
      int lineLength = startPoint.Position - line.Start.Position;
      int lineNumber = line.LineNumber;
      int nest = 0;

      Stack<IMappingTagSpan<LexTag>> tags = new Stack<IMappingTagSpan<LexTag>>();

      while(true)
      {
        var span = new SnapshotSpan(line.Start, lineLength);

        if(lineLength > 0)
        {
          foreach(var tagSpan in _aggregator.GetTags(span))
            tags.Push(tagSpan);
        }

        while(tags.Count > 0)
        {
          var tagSpan = tags.Pop();
          var tagType = tagSpan.Tag.type;

          if(_bracketTokens.ContainsKey(tagType))
          {
            Tuple<BracketSide, BracketGroup> bracket = _bracketTokens[tagType];

            if(bracket.Item2 == group)
            {
              switch(bracket.Item1)
              {
                case BracketSide.Start:
                  if(nest == 0)
                  {
                    yield return Mark(tagSpan, span);
                    yield break;
                  }

                  nest--;
                  break;

                case BracketSide.Intermediate:
                  if(nest == 0)
                    yield return Mark(tagSpan, span);

                  break;

                case BracketSide.End:
                  nest++;
                  break;
              }
            }
          }
        }

        if(lineNumber == 0)
          yield break;

        lineNumber--;
        line = line.Snapshot.GetLineFromLineNumber(lineNumber);
        lineLength = line.Length;
        tags.Clear();
      }
    }

    private TagSpan<TextMarkerTag> Mark(IMappingTagSpan<LexTag> tagSpan, SnapshotSpan snapshotSpan)
    {
      var tagSpans = tagSpan.Span.GetSpans(snapshotSpan.Snapshot);
      return new TagSpan<TextMarkerTag>(tagSpans[0], new TextMarkerTag("blue"));
    }
  }
}
