using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;


namespace Pony
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("Pony")]
  [TagType(typeof(LexTag))]
  public sealed class LexTagProvider : ITaggerProvider
  {
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return new LexTagger(buffer) as ITagger<T>;
    }
  }


  public class LexTag : ITag
  {
    public readonly TokenId type;

    public LexTag(TokenId type)
    {
      this.type = type;
    }
  }


  public sealed class LexTagger : ITagger<LexTag>, IDisposable
  {
    private readonly ITextBuffer _buffer;
    private readonly List<int> _lineStartState;
    private ITextSnapshot _lineCacheSnapshot;
    private int _maxLineProcessed;
    private readonly Lexer _lexer = new Lexer();
    private readonly object updateLock = new object();

    public LexTagger(ITextBuffer buffer)
    {
      _buffer = buffer;
      _lineCacheSnapshot = null;

      ITextSnapshot snapshot = _buffer.CurrentSnapshot;
      _lineStartState = new List<int>(snapshot.LineCount + 1);
      _lineStartState.Add(Lexer.normalState);

      for(int i = 1; i <= snapshot.LineCount; i++)
        _lineStartState.Add(Lexer.newState);

      _maxLineProcessed = -1;
      ProcessLine(0);
      _lineCacheSnapshot = snapshot;

      _buffer.Changed += TextBufferChanged;
    }

    public void Dispose()
    {
      _buffer.Changed -= TextBufferChanged;
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public IEnumerable<ITagSpan<LexTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      foreach(SnapshotSpan curSpan in spans)
      {
        if(curSpan.Snapshot != _lineCacheSnapshot)
          yield break;

        ITextSnapshotLine line = curSpan.Start.GetContainingLine();

        _lexer.HaveLine(line.GetText(), _lineStartState[line.LineNumber]);
        int location = line.Start;

        while(location < line.End)
        {
          int length;
          TokenId token = _lexer.Token(out length);

          if(token != TokenId.Ignore)
          {
            var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(location, length));
            if(tokenSpan.OverlapsWith(curSpan))
              yield return new TagSpan<LexTag>(tokenSpan, new LexTag(token));
          }

          location += length;
        }
      }
    }

    private void TextBufferChanged(object sender, TextContentChangedEventArgs e)
    {
      ITextSnapshot snapshot = _buffer.CurrentSnapshot;// e.After;

      // Add lines to and remove lines from line state list
      foreach(ITextChange change in e.Changes)
      {
        if(change.LineCountDelta > 0)
        {
          // Add new lines to line state list
          int line = snapshot.GetLineFromPosition(change.NewPosition).LineNumber;

          for(int i = 0; i < change.LineCountDelta; i++)
            _lineStartState.Insert(line + 1, Lexer.newState);
        }
        else if(change.LineCountDelta < 0)
        {
          // Remove lines from line state list
          int line = snapshot.GetLineFromPosition(change.NewPosition).LineNumber;
          _lineStartState.RemoveRange(line + 1, -change.LineCountDelta);
        }
      }

      // Now process any changed lines
      _maxLineProcessed = -1;
      List<SnapshotSpan> changedSpans = new List<SnapshotSpan>();

      foreach(ITextChange change in e.Changes)
      {
        ITextSnapshotLine startLine = snapshot.GetLineFromPosition(change.NewPosition);
        ProcessLine(startLine.LineNumber);

        ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber(_maxLineProcessed);
        changedSpans.Add(new SnapshotSpan(startLine.Start, endLine.End));
      }

      _lineCacheSnapshot = snapshot;

      // Our tags may have changed, tell anyone interested
      lock(updateLock)
      {
        var tempEvent = TagsChanged;

        if(tempEvent != null)
        {
          foreach(SnapshotSpan span in changedSpans)
            tempEvent(this, new SnapshotSpanEventArgs(span));
        }
      }
    }

    private void ProcessLine(int lineNo)
    {
      if(lineNo <= _maxLineProcessed) // Already processed this line
        return;

      int lineCount = _lineStartState.Count - 1;

      for(int line = lineNo; line < lineCount; line++)
      {
        string text = _buffer.CurrentSnapshot.GetLineFromLineNumber(line).GetText();
        _lexer.HaveLine(text, _lineStartState[line]);
        int endState = _lexer.EndState();

        _maxLineProcessed = line;

        if(endState == _lineStartState[line + 1]) // No knock-on to next line
          return;

        // Knock-on change to next line
        _lineStartState[line + 1] = endState;
      }
    }
  }
}
