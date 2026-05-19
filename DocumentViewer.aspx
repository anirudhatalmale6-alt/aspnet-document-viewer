<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="DocumentViewer.aspx.cs" Inherits="DocumentViewer.DocumentViewerPage" %>
<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Document Viewer</title>
    <style>
        /* ========== Reset & Base ========== */
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
            background: #f0f2f5;
            color: #1a1a2e;
            line-height: 1.5;
            min-height: 100vh;
        }

        /* ========== Header ========== */
        .header {
            background: #fff;
            border-bottom: 1px solid #e0e0e0;
            padding: 16px 24px;
            position: sticky;
            top: 0;
            z-index: 100;
            box-shadow: 0 1px 3px rgba(0,0,0,0.06);
        }
        .header-inner {
            max-width: 1400px;
            margin: 0 auto;
            display: flex;
            align-items: center;
            gap: 16px;
            flex-wrap: wrap;
        }
        .header h1 {
            font-size: 20px;
            font-weight: 600;
            color: #1a1a2e;
            white-space: nowrap;
        }
        .header h1 svg { vertical-align: middle; margin-right: 8px; }

        /* ========== Breadcrumb ========== */
        .breadcrumb {
            max-width: 1400px;
            margin: 0 auto;
            padding: 12px 24px;
            display: flex;
            align-items: center;
            flex-wrap: wrap;
            gap: 4px;
            font-size: 13px;
        }
        .breadcrumb a {
            color: #4361ee;
            text-decoration: none;
            padding: 2px 6px;
            border-radius: 4px;
            transition: background 0.15s;
        }
        .breadcrumb a:hover { background: #e8ecff; }
        .breadcrumb .sep { color: #999; margin: 0 2px; user-select: none; }
        .breadcrumb .current { color: #555; font-weight: 500; }

        /* ========== Toolbar ========== */
        .toolbar {
            max-width: 1400px;
            margin: 0 auto;
            padding: 0 24px 12px;
            display: flex;
            align-items: center;
            gap: 12px;
            flex-wrap: wrap;
        }
        .search-box {
            flex: 1;
            min-width: 200px;
            max-width: 400px;
            position: relative;
        }
        .search-box input {
            width: 100%;
            padding: 8px 12px 8px 36px;
            border: 1px solid #d0d5dd;
            border-radius: 8px;
            font-size: 14px;
            outline: none;
            transition: border-color 0.2s, box-shadow 0.2s;
            background: #fff;
        }
        .search-box input:focus {
            border-color: #4361ee;
            box-shadow: 0 0 0 3px rgba(67,97,238,0.12);
        }
        .search-box svg {
            position: absolute;
            left: 10px;
            top: 50%;
            transform: translateY(-50%);
            color: #999;
            pointer-events: none;
        }
        .sort-select {
            padding: 8px 12px;
            border: 1px solid #d0d5dd;
            border-radius: 8px;
            font-size: 14px;
            background: #fff;
            outline: none;
            cursor: pointer;
        }
        .sort-select:focus {
            border-color: #4361ee;
            box-shadow: 0 0 0 3px rgba(67,97,238,0.12);
        }
        .item-count {
            font-size: 13px;
            color: #777;
            margin-left: auto;
        }

        /* ========== Grid ========== */
        .grid-container {
            max-width: 1400px;
            margin: 0 auto;
            padding: 0 24px 32px;
        }
        .file-grid {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 16px;
        }
        @media (max-width: 1100px) { .file-grid { grid-template-columns: repeat(3, 1fr); } }
        @media (max-width: 800px) { .file-grid { grid-template-columns: repeat(2, 1fr); } }
        @media (max-width: 520px) { .file-grid { grid-template-columns: 1fr; } }

        /* ========== Tile ========== */
        .tile {
            background: #fff;
            border: 1px solid #e8e8e8;
            border-radius: 12px;
            padding: 20px 16px 14px;
            display: flex;
            flex-direction: column;
            align-items: center;
            text-align: center;
            transition: transform 0.15s, box-shadow 0.15s, border-color 0.15s;
            cursor: default;
            position: relative;
        }
        .tile:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(0,0,0,0.08);
            border-color: #c8c8c8;
        }
        .tile.folder-tile { cursor: pointer; }
        .tile.folder-tile:hover { border-color: #4361ee; }

        .tile-icon {
            width: 56px;
            height: 56px;
            margin-bottom: 12px;
            flex-shrink: 0;
        }
        .tile-icon svg { width: 100%; height: 100%; }

        .tile-name {
            font-size: 13px;
            font-weight: 500;
            color: #1a1a2e;
            word-break: break-word;
            line-height: 1.3;
            max-height: 2.6em;
            overflow: hidden;
            display: -webkit-box;
            -webkit-line-clamp: 2;
            -webkit-box-orient: vertical;
            margin-bottom: 6px;
            width: 100%;
        }
        .tile-name a {
            color: inherit;
            text-decoration: none;
        }
        .tile-name a:hover { color: #4361ee; text-decoration: underline; }

        .tile-meta {
            font-size: 11px;
            color: #888;
            margin-bottom: 10px;
        }
        .tile-meta span + span::before { content: " \00b7 "; }

        .tile-actions {
            display: flex;
            gap: 6px;
            width: 100%;
        }
        .btn {
            flex: 1;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 4px;
            padding: 6px 10px;
            border: 1px solid #d0d5dd;
            border-radius: 6px;
            font-size: 12px;
            font-weight: 500;
            text-decoration: none;
            color: #444;
            background: #fafafa;
            transition: background 0.15s, border-color 0.15s;
            cursor: pointer;
            white-space: nowrap;
        }
        .btn:hover { background: #f0f0f0; border-color: #bbb; }
        .btn svg { width: 14px; height: 14px; flex-shrink: 0; }
        .btn-primary {
            background: #4361ee;
            color: #fff;
            border-color: #4361ee;
        }
        .btn-primary:hover { background: #3451d1; border-color: #3451d1; }

        /* ========== Empty / Error ========== */
        .empty-state, .error-state {
            text-align: center;
            padding: 60px 20px;
            color: #888;
        }
        .empty-state svg, .error-state svg { margin-bottom: 16px; opacity: 0.4; }
        .empty-state h3, .error-state h3 { font-size: 18px; margin-bottom: 6px; color: #555; }
        .error-state { color: #c0392b; }
        .error-state h3 { color: #c0392b; }

        /* ========== Folder count badge ========== */
        .folder-badge {
            font-size: 11px;
            color: #666;
            background: #f0f0f0;
            padding: 1px 8px;
            border-radius: 10px;
            margin-bottom: 10px;
        }

        /* ========== Scrollbar ========== */
        ::-webkit-scrollbar { width: 8px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: #ccc; border-radius: 4px; }
        ::-webkit-scrollbar-thumb:hover { background: #aaa; }
    </style>
</head>
<body>
    <form id="mainForm" runat="server">
        <!-- Header -->
        <div class="header">
            <div class="header-inner">
                <h1>
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#4361ee" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                        <polyline points="14 2 14 8 20 8"/>
                        <line x1="16" y1="13" x2="8" y2="13"/>
                        <line x1="16" y1="17" x2="8" y2="17"/>
                        <polyline points="10 9 9 9 8 9"/>
                    </svg>
                    Document Viewer
                </h1>
            </div>
        </div>

        <!-- Breadcrumb -->
        <div class="breadcrumb">
            <asp:Literal ID="litBreadcrumb" runat="server" />
        </div>

        <!-- Toolbar -->
        <div class="toolbar">
            <div class="search-box">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
                </svg>
                <asp:TextBox ID="txtSearch" runat="server" placeholder="Search files and folders..." AutoPostBack="true" OnTextChanged="txtSearch_TextChanged" />
            </div>
            <asp:DropDownList ID="ddlSort" runat="server" CssClass="sort-select" AutoPostBack="true" OnSelectedIndexChanged="ddlSort_SelectedIndexChanged">
                <asp:ListItem Value="name_asc" Text="Name A-Z" />
                <asp:ListItem Value="name_desc" Text="Name Z-A" />
                <asp:ListItem Value="date_desc" Text="Newest first" />
                <asp:ListItem Value="date_asc" Text="Oldest first" />
                <asp:ListItem Value="size_desc" Text="Largest first" />
                <asp:ListItem Value="size_asc" Text="Smallest first" />
            </asp:DropDownList>
            <span class="item-count"><asp:Literal ID="litItemCount" runat="server" /></span>
        </div>

        <!-- Grid -->
        <div class="grid-container">
            <asp:Literal ID="litGrid" runat="server" />
        </div>
    </form>
</body>
</html>
