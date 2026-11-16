# changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

First release of the GLADOS SOLIDWORKS Add-in.

### Overview

- A task pane in SOLIDWORKS that compares the open document's custom properties with the GLADOS catalogue (bought and made items) and shows whether the document matches its catalogue item, diverges from it, or has none yet.
- Edits the add-in-owned custom properties of bought items, made items (including the routing in `Glados.Processes`), customer items and made assemblies. Every edit goes into the SOLIDWORKS document, at document level and in every configuration; the catalogue item itself still comes from PDM ingest. See `README.md` for the sync strategy and the property convention.
- Resolves which custom property backs each GLADOS field through the field mapping synced from the server, with compiled-in defaults before the first sync.
- Validates a document against GLADOS' PDM ingest rules before release and warns about references into a Downloads folder.
- Fills a bought item from the EPLAN Data Portal when GLADOS has the portal switched on.
- Creates a missing source item from the made-item source picker, the one write the add-in makes to GLADOS.
- Keeps working offline from a cached snapshot of the lookup lists and the field mapping.
- Warns in the task pane when the backend serves a `pat` contract other than the supported one, without blocking anything.
- English and German (de-AT) user interface, following the GLADOS user's language.
- A daily diagnostics log under `%LOCALAPPDATA%\GladosSwAddin\logs` (14 days kept), reachable from **Settings → Open log folder**.

### Requirements

- 64-bit Windows with SOLIDWORKS 2025 or later.
- .NET Framework 4.8 or later; the installer refuses to run without it.
- Administrator rights to install: the MSI installs per machine under `Program Files\DELOARTS\GLADOS SOLIDWORKS Add-in` and sets the add-in to load at startup for the installing user. See [docs/RELEASE.md](docs/RELEASE.md) for install, upgrade and uninstall.

### Compatibility

- Supports backend 0.1.x (`pat` contract 0.1).
- The personal access token needs `write:source_items` to create source items from the made-item picker and `use:data_portal` for the EPLAN Data Portal lookups. Every other call only needs a valid token. The SolidWorks add-in token preset in GLADOS grants both.
