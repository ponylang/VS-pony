using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;


namespace Pony
{
  [ClassInterface(ClassInterfaceType.AutoDual)]
  [CLSCompliant(false), ComVisible(true)]
  public class OptionPage : DialogPage
  {
    private int _indentSize;
    private string _compilerPath = "";
    private string _srcPath = "";

    [Category("Editor")]
    [DisplayName("Indent size")]
    [Description("Characters per indent level")]
    public int IndentSize
    {
      get { return _indentSize; }
      set { _indentSize = value; }
    }

    [Category("Paths")]
    [DisplayName("Compiler")]
    [Description("Path of compiler exe")]
    public string CompilerPath
    {
      get { return _compilerPath; }
      set { _compilerPath = value; }
    }

    [Category("Paths")]
    [DisplayName("Source")]
    [Description("Path of project source code")]
    public string SrcPath
    {
      get { return _srcPath; }
      set { _srcPath = value; }
    }


    protected override void OnActivate(CancelEventArgs e)
    {
      base.OnActivate(e);

      var options = GetOptions();
      IndentSize = options.GetIndentSize();
      CompilerPath = options.GetCompilerPath();
      SrcPath = options.GetSrcPath();
    }

    protected override void OnApply(PageApplyEventArgs args)
    {
      if(args.ApplyBehavior == ApplyKind.Apply)
      {
        var options = GetOptions();
        options.Update(IndentSize, CompilerPath, SrcPath);
      }

      base.OnApply(args);
    }

    private Options GetOptions()
    {
      var componentModel = (IComponentModel)(Site.GetService(typeof(SComponentModel)));
      return componentModel.DefaultExportProvider.GetExportedValue<Options>();
    }
  }
}
