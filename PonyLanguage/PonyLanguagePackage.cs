using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;


namespace Pony
{
  [PackageRegistration(UseManagedResourcesOnly = true)]
  [InstalledProductRegistration("#110", "#112", "0.1", IconResourceID = 400)]
  [ProvideMenuResource("Menus.ctmenu", 1)]
  [Guid(GuidList.guidPonyLanguagePkgString)]
  [ProvideOptionPage(typeof(OptionPage), "Pony", "General", 0, 0, true)]
  public sealed class PonyLanguagePackage : Package
  {
    // Constructor.
    // Should only contain code that doesn't require any Visual Studio services.
    public PonyLanguagePackage()
    {}

    // Initialiser called once Visual Studio services are up and running
    protected override void Initialize()
    {
      base.Initialize();

      // Add our command handlers for menu (commands must exist in the .vsct file)
      OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

      if(null != mcs)
      {
        CommandID menuCommandID = new CommandID(GuidList.guidPonyLanguageCmdSet, (int)PkgCmdIDList.cmdidCheckErrors);
        MenuCommand menuItem = new MenuCommand(CheckErrorsCallback, menuCommandID);
        mcs.AddCommand(menuItem);

        menuCommandID = new CommandID(GuidList.guidPonyLanguageCmdSet, (int)PkgCmdIDList.cmdidClearErrors);
        menuItem = new MenuCommand(ClearErrorsCallback, menuCommandID);
        mcs.AddCommand(menuItem);
      }
    }

    private void CheckErrorsCallback(object sender, EventArgs e)
    {
      GetErrorBuilder().ProcessErrors();
    }

    private void ClearErrorsCallback(object sender, EventArgs e)
    {
      GetErrorBuilder().ClearErrors();
    }

    private ErrorBuilder GetErrorBuilder()
    {
      var componentModel = (IComponentModel)(GetService(typeof(SComponentModel)));
      return componentModel.DefaultExportProvider.GetExportedValue<ErrorBuilder>();
    }
  }
}
