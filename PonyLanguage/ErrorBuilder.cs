using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.RegularExpressions;


namespace Pony
{
  public sealed class ErrorInfo
  {
    public readonly string filename;
    public readonly int line;
    public readonly int pos_on_line;
    public readonly string text;
    public int pos_in_file;
    public int length;

    public ErrorInfo(string filename, int line, int pos_on_line, string text)
    {
      this.filename = filename;
      this.line = line;
      this.pos_on_line = pos_on_line;
      this.text = text;
      pos_in_file = 0;
      length = 0;
    }

    public ErrorInfo(ErrorInfo other)
    {
      filename = other.filename;
      line = other.line;
      pos_on_line = other.pos_on_line;
      text = other.text;
      pos_in_file = other.pos_in_file;
      length = other.length;
    }
  }


  [Export]
  public class ErrorBuilder
  {
    [Import]
    internal Options _options = null;

    private readonly List<ErrorInfo> _errors = new List<ErrorInfo>();
    private readonly object _updateLock = new object();

    public ErrorBuilder()
    {}

    public event Action Changed;

    public void ProcessErrors()
    {
      _errors.Clear();

      // Run compiler and get output
      System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
      pProcess.StartInfo.FileName = _options.GetCompilerPath();
      pProcess.StartInfo.Arguments = _options.GetSrcPath();
      pProcess.StartInfo.UseShellExecute = false;
      pProcess.StartInfo.CreateNoWindow = true;
      pProcess.StartInfo.RedirectStandardOutput = true;
      pProcess.Start();
      string output = pProcess.StandardOutput.ReadToEnd();
      pProcess.WaitForExit();

      // Extract error messages from output
      Regex regex = new Regex(@"^([A-Z]:\\[^:]*\.pony):([0-9]+):([0-9]+): (.*)$");

      using(StringReader sr = new StringReader(output))
      {
        string line;

        while((line = sr.ReadLine()) != null)
        {
          Match m = regex.Match(line);

          if(m.Success)
          {
            string filename = m.Groups[1].Value;
            int line_no = 0;
            Int32.TryParse(m.Groups[2].Value, out line_no);
            int pos = 0;
            Int32.TryParse(m.Groups[3].Value, out pos);
            string message = m.Groups[4].Value;

            // In error messages line numbers and positions are 1 based, in ITextBuffers they're 0 based
            line_no -= 1;
            pos -= 1;

            _errors.Add(new ErrorInfo(filename, line_no, pos, message));
          }
        }
      }

      Update();
    }

    public void ClearErrors()
    {
      if(_errors.Count == 0)  // Nothing to clear
        return;

      _errors.Clear();
      Update();
    }

    public void GetErrors(string filename, List<ErrorInfo> errors)
    {
      foreach(var error in _errors)
      {
        if(error.filename == filename)
          errors.Add(new ErrorInfo(error));
      }
    }

    private void Update()
    {
      lock(_updateLock)
      {
        var tempEvent = Changed;

        if(tempEvent != null)
          tempEvent();
      }
    }
  }
}
