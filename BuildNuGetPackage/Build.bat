REM @echo off

%~d0
cd "%~p0"

del *.nu*
del *.dll
del *.pdb
del *.xml
del *.ps1

copy ..\ProductiveRage.Immutable\bin\Release\ProductiveRage.Immutable.dll > nul

copy ..\ProductiveRage.Immutable.Analyser\Analyser\bin\Release\* > nul
copy ..\ProductiveRage.Immutable.Analyser\Analyser\bin\Release\tools\* > nul

copy ..\ProductiveRage.Immutable.nuspec > nul
..\packages\NuGet.CommandLine.2.8.5\tools\nuget pack -NoPackageAnalysis ProductiveRage.Immutable.nuspec