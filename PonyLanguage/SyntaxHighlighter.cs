using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Language.StandardClassification;


namespace Pony
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("Pony")]
  [TagType(typeof(ClassificationTag))]
  public sealed class SyntaxHighlighterProvider : ITaggerProvider
  {
    [Export]
    [Name("Pony")]
    [BaseDefinition("code")]
    internal static ContentTypeDefinition PonyContentType = null;

    [Export]
    [FileExtension(".pony")]
    [ContentType("Pony")]
    internal static FileExtensionToContentTypeDefinition PonyFileType = null;

    [Import]
    internal IClassificationTypeRegistryService _classificationTypeRegistry = null;

    [Import]
    internal IBufferTagAggregatorFactoryService _aggregatorFactory = null;

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      ITagAggregator<LexTag> lexTagAggregator = _aggregatorFactory.CreateTagAggregator<LexTag>(buffer);
      return new SyntaxHighlighter(buffer, lexTagAggregator, _classificationTypeRegistry) as ITagger<T>;
    }
  }


  public sealed class SyntaxHighlighter : ITagger<ClassificationTag>, IDisposable
  {
    private readonly ITextBuffer _buffer;
    private readonly ITagAggregator<LexTag> _lexTags;
    private readonly IDictionary<TokenId, IClassificationType> _colourMap = new Dictionary<TokenId, IClassificationType>();
    private readonly object _updateLock = new object();

    private const string ClassificationType = "cppType";
    private const string ClassificationIdentifier = "cppLocalVariable";

    public SyntaxHighlighter(ITextBuffer buffer, ITagAggregator<LexTag> lexTagAggregator, IClassificationTypeRegistryService typeService)
    {
      _buffer = buffer;
      _lexTags = lexTagAggregator;

      _colourMap[TokenId.Comment] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Comment);
      _colourMap[TokenId.Number] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Number);
      _colourMap[TokenId.String] = typeService.GetClassificationType(PredefinedClassificationTypeNames.String);
      _colourMap[TokenId.TypeID] = typeService.GetClassificationType(ClassificationType);
      _colourMap[TokenId.VarID] = typeService.GetClassificationType(ClassificationIdentifier);
      _colourMap[TokenId.InfixOp] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);

      _colourMap[TokenId.DontCare] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Literal);
      _colourMap[TokenId.TrueFalse] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Literal);
      _colourMap[TokenId.Intrinsic] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Use] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Type] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Interface] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Trait] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Primitive] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Struct] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Class] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Actor] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Object] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Lambda] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Delegate] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.As] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Is] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Isnt] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Var] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Let] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Embed] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.New] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Fun] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Be] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Capability] = typeService.GetClassificationType(ClassificationType);
      _colourMap[TokenId.This] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Return] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Break] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Continue] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Error] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.CompileError] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Consume] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Recover] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.If] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Ifdef] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Then] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Else] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.ElseIf] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.End] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.While] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Do] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Repeat] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Until] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.For] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.In] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Match] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Where] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Try] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.With] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      _colourMap[TokenId.Not] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.And] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.Or] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.Xor] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.IdentityOf] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.AddressOf] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);

      _colourMap[TokenId.Ellipsis] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.Arrow] = typeService.GetClassificationType(ClassificationType);
      _colourMap[TokenId.DoubleArrow] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.LBrace] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.RBrace] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.LParen] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.RParen] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.LSquare] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.RSquare] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.Comma] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.Dot] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.Tilde] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.Colon] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.Semi] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);
      _colourMap[TokenId.Assign] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.At] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.Ampersand] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Operator);
      _colourMap[TokenId.Ephemeral] = typeService.GetClassificationType(ClassificationType);
      _colourMap[TokenId.Borrowed] = typeService.GetClassificationType(ClassificationType);
      _colourMap[TokenId.Question] = typeService.GetClassificationType(PredefinedClassificationTypeNames.Other);

      _lexTags.TagsChanged += LexTagChanged;
    }

    public void Dispose()
    {
      _lexTags.TagsChanged -= LexTagChanged;
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      var tagsChanged = TagsChanged;

      foreach(var tagSpan in this._lexTags.GetTags(spans))
      {
        var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
        yield return new TagSpan<ClassificationTag>(tagSpans[0], new ClassificationTag(_colourMap[tagSpan.Tag.type]));
      }
    }

    private void LexTagChanged(object sender, TagsChangedEventArgs e)
    {
      lock(_updateLock)
      {
        var tempEvent = TagsChanged;

        if(tempEvent != null)
        {
          foreach(var span in e.Span.GetSpans(_buffer))
            tempEvent(this, new SnapshotSpanEventArgs(span));
        }
      }
    }
  }
}
