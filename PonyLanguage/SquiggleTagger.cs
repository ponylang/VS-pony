using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;


namespace Pony
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("Pony")]
  [TagType(typeof(IErrorTag))]
  public sealed class SquiggleTaggerProvider : ITaggerProvider
  {
    [Import]
    internal ErrorBuilder _errorBuilder = null;

    [Import]
    internal IBufferTagAggregatorFactoryService _aggregatorFactory = null;

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      ITagAggregator<LexTag> lexTagAggregator = _aggregatorFactory.CreateTagAggregator<LexTag>(buffer);
      return new SquiggleTagger(buffer, _errorBuilder, lexTagAggregator) as ITagger<T>;
    }
  }


  public sealed class SquiggleTagger : ITagger<IErrorTag>, IDisposable
  {
    private readonly ITextBuffer _buffer;
    private readonly ErrorBuilder _errorBuilder;
    private readonly ITagAggregator<LexTag> _lexTags;
    private readonly string _filename;
    private ITextSnapshot _currentSnapshot;
    private readonly List<ErrorInfo> _squiggles = new List<ErrorInfo>();
    private readonly object _updateLock = new object();

    public SquiggleTagger(ITextBuffer buffer, ErrorBuilder errorBuilder, ITagAggregator<LexTag> lexTagger)
    {
      _buffer = buffer;
      _errorBuilder = errorBuilder;
      _lexTags = lexTagger;
      _filename = FindFilename(_buffer);
      _currentSnapshot = _buffer.CurrentSnapshot;

      _buffer.Changed += TextBufferChanged;
      _errorBuilder.Changed += ErrorsChanged;

      FetchErrors();
    }

    public void Dispose()
    {
      _buffer.Changed -= TextBufferChanged;
      _errorBuilder.Changed -= ErrorsChanged;
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      if(spans[0].Snapshot != _currentSnapshot)
        yield break;

      foreach(var squiggle in _squiggles)
      {
        var mappedSpan = new SnapshotSpan(spans[0].Snapshot, new Span(squiggle.pos_in_file, squiggle.length));
        if((mappedSpan.Length != 0) && spans.IntersectsWith(new NormalizedSnapshotSpanCollection(mappedSpan)))
        {
          yield return new TagSpan<ErrorTag>(mappedSpan, new ErrorTag("Compile error", squiggle.text));
        }
      }
    }

    private static string FindFilename(ITextBuffer buffer)
    {
      IVsTextBuffer bufferAdapter;
      buffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out bufferAdapter);
      string filename = "Unknown";

      if(bufferAdapter == null)
        return filename;

      var persistFileFormat = bufferAdapter as IPersistFileFormat;
      uint format;

      if(persistFileFormat != null)
        persistFileFormat.GetCurFile(out filename, out format);

      return filename;
    }

    private void TextBufferChanged(object sender, TextContentChangedEventArgs e)
    {
      // Clear all error tags from all files
      _currentSnapshot = _buffer.CurrentSnapshot;
      _errorBuilder.ClearErrors();
    }

    private void ErrorsChanged()
    {
      FetchErrors();
    }

    private void FetchErrors()
    {
      _squiggles.Clear();
      _errorBuilder.GetErrors(_filename, _squiggles);

      // Determine location info for each error
      foreach(var squiggle in _squiggles)
      {
        int line_start = _currentSnapshot.GetLineFromLineNumber(squiggle.line).Start;
        squiggle.pos_in_file = line_start + squiggle.pos_on_line;
        squiggle.length = 1;

        var errorPoint = new SnapshotSpan(_currentSnapshot, new Span(squiggle.pos_in_file, 1));

        foreach(var tag in _lexTags.GetTags(errorPoint))
        {
          var tagSpans = tag.Span.GetSpans(_currentSnapshot);
          squiggle.length = tagSpans[0].Length;
        }
      }

      Update();
    }

    private void Update()
    {
      lock(_updateLock)
      {
        var tempEvent = TagsChanged;

        if(tempEvent != null)
          tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0,
              _buffer.CurrentSnapshot.Length)));
      }
    }
  }
}
