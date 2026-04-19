# Services Analysis Report

## Overview
The `Services` directory contains three .NET projects:

- **CommandParser** – A lightweight command parsing utility.
- **SimpleStore** – An in‑memory key‑value store for byte arrays.
- **TestServices** – Unit tests for the CommandParser.

## 1. CommandParser (`CommandParser/`)
- **Purpose**: Parses a command string into three components: `Command`, `Key`, and `Value`.

## 2. SimpleStore (`SimpleStore/`)
- **Purpose**: Simple in‑memory dictionary‑based storage.
- **API**:
  - `Set(string key, byte[] value)` – stores a value.
  - `Get(string key)` – retrieves a value (returns `null` if missing).
  - `Delete(string key)` – removes a key.

## 3. TestServices (`TestServices/`)
- **Purpose**: Unit tests for `CommandParser.Parse`.
- **Testing Framework**: xUnit.
- **Dependencies**:
  - References `CommandParser` project.
  - Uses `Microsoft.NET.Test.Sdk`, `xunit`, etc.

## Relationships
- `TestServices` depends on `CommandParser`.
- `SimpleStore` is independent; could be used as a storage backend.
- All projects target .NET 10.0.
