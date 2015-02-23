﻿using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;


namespace Pony
{
  [Export]
  public class Options
  {
    private const string CollectionPath = "Settings";
    private  const string IndentSizeName = "IndentSize";
    private const string CompilerPathName = "CompilerPath";
    private const string SrcPathName = "SrcPath";

    private int _indentSize = 2;
    private string _compilerPath = "";
    private string _srcPath = "";

    private readonly WritableSettingsStore _writableSettingsStore;

    [ImportingConstructor]
    public Options(SVsServiceProvider vsServiceProvider)
    {
      var shellSettingsManager = new ShellSettingsManager(vsServiceProvider);
      _writableSettingsStore = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
      LoadSettings();
    }

    public int GetIndentSize()
    {
      return _indentSize;
    }

    public string GetCompilerPath()
    {
      return _compilerPath;
    }

    public string GetSrcPath()
    {
      return _srcPath;
    }

    public void Update(int indentSize, string compilerPath, string srcPath)
    {
      _indentSize = indentSize;
      _compilerPath = compilerPath;
      _srcPath = srcPath;
      SaveSettings();
    }

    private void LoadSettings()
    {
      try
      {
        _indentSize = _writableSettingsStore.GetInt32(CollectionPath, IndentSizeName, _indentSize);
        _compilerPath = _writableSettingsStore.GetString(CollectionPath, CompilerPathName, _compilerPath);
        _srcPath = _writableSettingsStore.GetString(CollectionPath, SrcPathName, _srcPath);
      }
      catch(Exception ex)
      {
        Debug.Fail(ex.Message);
      }
    }

    private void SaveSettings()
    {
      try
      {
        _writableSettingsStore.CreateCollection(CollectionPath);
        _writableSettingsStore.SetInt32(CollectionPath, IndentSizeName, _indentSize);
        _writableSettingsStore.SetString(CollectionPath, CompilerPathName, _compilerPath);
        _writableSettingsStore.SetString(CollectionPath, SrcPathName, _srcPath);
      }
      catch(Exception ex)
      {
        Debug.Fail(ex.Message);
      }
    }
  }
}
