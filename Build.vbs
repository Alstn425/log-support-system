Option Explicit

Dim fs, sh, folder, compiler, windows, sources, source
Dim command, code, stage, output, log, problem, number
Dim process, textOut, textErr, logFile
Set fs = CreateObject("Scripting.FileSystemObject")
Set sh = CreateObject("WScript.Shell")
folder = fs.GetParentFolderName(WScript.ScriptFullName)
sh.CurrentDirectory = folder
windows = sh.ExpandEnvironmentStrings("%WINDIR%")
compiler = fs.BuildPath(windows, "Microsoft.NET\Framework\v4.0.30319\csc.exe")
If Not fs.FileExists(compiler) Then
    compiler = fs.BuildPath(windows, "Microsoft.NET\Framework64\v4.0.30319\csc.exe")
End If
If Not fs.FileExists(compiler) Then
    MsgBox "C# compiler not found.", vbCritical, "Build"
    WScript.Quit 1
End If

sources = Array("Program.cs", "Core.cs", "Dialogs.cs", "MainForm.cs")
For Each source In sources
    If Not fs.FileExists(fs.BuildPath(folder, source)) Then
        MsgBox "Missing source: " & source, vbCritical, "Build"
        WScript.Quit 1
    End If
Next

On Error Resume Next
If Not fs.FolderExists(fs.BuildPath(folder, "_build")) Then
    fs.CreateFolder fs.BuildPath(folder, "_build")
End If
number = Err.Number
problem = Err.Description
On Error GoTo 0
If number <> 0 Then
    MsgBox "Build folder error " & number & ": " & problem, vbCritical, "Build"
    WScript.Quit 1
End If
stage = "_build\CoastNotebook.build.exe"
output = "CoastNotebook.exe"
log = "Build.log"

command = Q(compiler) & " /nologo /target:winexe /langversion:5"
command = command & " /platform:anycpu /codepage:65001 /warnaserror+"
command = command & " /reference:System.Windows.Forms.dll"
command = command & " /reference:System.Drawing.dll"
command = command & " /reference:System.Xml.dll"
command = command & " /reference:System.Core.dll"
command = command & " /out:" & Q(stage)
For Each source In sources
    command = command & " " & Q(source)
Next

On Error Resume Next
Set process = sh.Exec(command)
number = Err.Number
problem = Err.Description
On Error GoTo 0
If number <> 0 Then
    MsgBox "Compiler start error " & number & ": " & problem, vbCritical, "Build"
    WScript.Quit 1
End If

textOut = process.StdOut.ReadAll()
textErr = process.StdErr.ReadAll()
Do While process.Status = 0
    WScript.Sleep 100
Loop
code = process.ExitCode

On Error Resume Next
Set logFile = fs.CreateTextFile(fs.BuildPath(folder, log), True, True)
logFile.WriteLine "Compiler exit code: " & code
logFile.WriteLine textOut
logFile.WriteLine textErr
logFile.Close
number = Err.Number
problem = Err.Description
On Error GoTo 0
If number <> 0 Then
    MsgBox "Build log error " & number & ": " & problem, vbCritical, "Build"
    WScript.Quit 1
End If

If code <> 0 Or Not fs.FileExists(fs.BuildPath(folder, stage)) Then
    MsgBox "Build failed. Open Build.log. The previous EXE was not replaced.", vbCritical, "Build"
    WScript.Quit 1
End If

On Error Resume Next
fs.CopyFile fs.BuildPath(folder, stage), fs.BuildPath(folder, output), True
number = Err.Number
problem = Err.Description
If number <> 0 Then
    On Error GoTo 0
    MsgBox "EXE replacement error " & number & ": " & problem _
        & vbCrLf & "Close the running program and retry.", vbCritical, "Build"
    WScript.Quit 1
End If
On Error GoTo 0

MsgBox "Build completed: CoastNotebook.exe", vbInformation, "Build"

Function Q(value)
    Q = Chr(34) & value & Chr(34)
End Function
