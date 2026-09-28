#pragma once
// Hover tooltips are read-only. Panels/dialogs/maps still block transactions.
static bool action_ui_blocked(unsigned command,unsigned flags){return (flags&((command==14||command==19||command==21)?~8u:~0u))!=0;}
