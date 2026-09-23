using Autodesk.AutoCAD.Runtime;

// Both command classes are registered here so adding a class does not mean
// remembering to add another assembly attribute somewhere else.
[assembly: CommandClass(typeof(GmpPlatform.Acad.TicketDrawCommands))]
[assembly: CommandClass(typeof(GmpPlatform.Acad.TicketPlotCommands))]
