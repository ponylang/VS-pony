using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;


namespace Pony
{
  [Export(typeof(ISmartIndentProvider))]
  [ContentType("Pony")]
  public class IndentProvider : ISmartIndentProvider
  {
    [Import]
    internal Options _options = null;

    [Import]
    internal IViewTagAggregatorFactoryService _aggregatorFactory = null;

    public ISmartIndent CreateSmartIndent(ITextView textView)
    {
      ITagAggregator<LexTag> lexTagAggregator = _aggregatorFactory.CreateTagAggregator<LexTag>(textView);
      return new Indenter(textView, _options, lexTagAggregator);
    }
  }


  public enum IndentChange
  {
    None,   // No affect
    Inc,    // Increases indent, eg "if"
    Dec,    // Decreases indent, eg "end"
    BackOne // Pushes line back one level if at start, eg "then"
  }


  public class Indenter : ISmartIndent, IDisposable
  {
    private readonly ITextView _view;
    private readonly Options _options;
    private readonly ITagAggregator<LexTag> _lexTags;
    private readonly IDictionary<TokenId, IndentChange> _indenters = new Dictionary<TokenId, IndentChange>();

    public Indenter(ITextView view, Options options, ITagAggregator<LexTag> lexTagAggregator)
    {
      _view = view;
      _options = options;
      _lexTags = lexTagAggregator;

      _indenters[TokenId.Type] = IndentChange.Inc;
      _indenters[TokenId.Interface] = IndentChange.Inc;
      _indenters[TokenId.Trait] = IndentChange.Inc;
      _indenters[TokenId.Primitive] = IndentChange.Inc;
      _indenters[TokenId.Class] = IndentChange.Inc;
      _indenters[TokenId.Actor] = IndentChange.Inc;
      _indenters[TokenId.Object] = IndentChange.Inc;
      _indenters[TokenId.New] = IndentChange.Inc;
      _indenters[TokenId.Fun] = IndentChange.Inc;
      _indenters[TokenId.Be] = IndentChange.Inc;
      _indenters[TokenId.Recover] = IndentChange.Inc;
      _indenters[TokenId.If] = IndentChange.Inc;
      _indenters[TokenId.Where] = IndentChange.Inc;
      _indenters[TokenId.Repeat] = IndentChange.Inc;
      _indenters[TokenId.Until] = IndentChange.Inc;
      _indenters[TokenId.For] = IndentChange.Inc;
      _indenters[TokenId.Match] = IndentChange.Inc;
      _indenters[TokenId.Trait] = IndentChange.Inc;
      _indenters[TokenId.With] = IndentChange.Inc;
      _indenters[TokenId.End] = IndentChange.Dec;
      _indenters[TokenId.DoubleArrow] = IndentChange.BackOne;
      _indenters[TokenId.Then] = IndentChange.BackOne;
      _indenters[TokenId.Else] = IndentChange.BackOne;
      _indenters[TokenId.ElseIf] = IndentChange.BackOne;
      _indenters[TokenId.Do] = IndentChange.BackOne;
    }

    public int? GetDesiredIndentation(ITextSnapshotLine line)
    {
      // Look at the previous line
      int line_no = line.LineNumber - 1;
      var snapshot = line.Snapshot;

      while(line_no >= 0)
      {
        var prevLine = snapshot.GetLineFromLineNumber(line_no);
        var lineSpan = new SnapshotSpan(snapshot, new Span(prevLine.Start, prevLine.Length));
        int prevIndent = 0;
        bool nonBlank = false;
        int indentInc = 0;

        foreach(var tag in _lexTags.GetTags(lineSpan))
        {
          TokenId id = tag.Tag.type;

          if(id == TokenId.Ignore || id == TokenId.Comment) // Ignore comments and whitespace
            continue;

          IndentChange change = IndentChange.None;
          _indenters.TryGetValue(id, out change);

          if(!nonBlank)
          {
            var tagSpans = tag.Span.GetSpans(snapshot);
            prevIndent = tagSpans[0].Start - prevLine.Start;
            nonBlank = true;

            if(change == IndentChange.Dec || change == IndentChange.BackOne)
            {
              // Our 0 change position should be one level in from previous line
              indentInc++;
            }
          }

          if(change == IndentChange.Inc)
            indentInc++;

          if(change == IndentChange.Dec)
            indentInc--;
        }

        if(nonBlank)
        {
          // We found a non-blank line
          int newIndent = prevIndent + (indentInc * _options.GetIndentSize());

          if(newIndent < 0)
            return 0;

          return newIndent;
        }

        // That line was blank, try the one before
        line_no--;
      }

      // No non-blank lines before this one, no indent
      return 0;
    }

    public void Dispose()
    { }
  }
}
