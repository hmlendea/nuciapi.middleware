# Contributing to NuciAPI.Middleware

This document covers the guidelines and processes for contributing to this project, including how to report issues, suggest enhancements, submit code changes, and follow project standards.

## 📑 Table of Contents

- [How to Contribute](#-how-to-contribute)
- [Code of Conduct](#-code-of-conduct)
- [Security](#-security)
- [License](#-license)
- [Recognition](#-recognition)
- [Getting Assistance](#-getting-assistance)

## 🤝 How to Contribute

### Reporting Issues

- Search existing issues first.
- Use the issue templates if available.
- Provide clear reproduction steps.
- Include environment details.

### Suggesting Enhancements

- Check the roadmap and existing discussions.
- Explain the use case and expected behaviour.
- Consider implementation complexity.

### Code Contributions

#### Prerequisites

- .NET SDK 10.0 or later
- Git

#### Development Setup

```bash
# Clone the repository
git clone https://github.com/hmlendea/nuciapi.middleware.git
cd nuciapi.middleware

# Install dependencies
dotnet restore NuciAPI.Middleware.sln
```

#### Making Changes

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Make your changes.
4. Run tests: `dotnet test NuciAPI.Middleware.sln`
5. Run linters: `dotnet format NuciAPI.Middleware.sln --verify-no-changes`
6. Commit with clear and descriptive messages.
7. Push to your fork.
8. Open a Pull Request.

#### Pull Request Guidelines

- Target the `master` branch.
- Keep PRs focused and atomic.
- Revise documentation if applicable.
- Include tests for any new functionality.
- Ensure the CI checks pass.

### Code Style

Follow the project's coding standards:
- C# coding conventions as defined in the repository instructions
- Run `dotnet format NuciAPI.Middleware.sln` before committing.

### Testing

```bash
# Run all tests
dotnet test NuciAPI.Middleware.sln

# Run specific test suite
dotnet test NuciAPI.Middleware.UnitTests/NuciAPI.Middleware.UnitTests.csproj
```

### Documentation

- Revise relevant docs for changes.
- Follow the documentation style guide.
- Preview changes locally if possible.

## 📋 Code of Conduct

This project follows the [Code of Conduct](./CODE_OF_CONDUCT.md).

## 🔒 Security

Report security vulnerabilities per the [Security Policy](./SECURITY.md).

## 📄 License

By contributing, you agree that your contributions will be licensed under the GNU General Public License v3.0 or later.

## 🙏 Recognition

Contributors are recognized in the GitHub contributors list.

## ❓ Getting Assistance

- Open a GitHub issue for questions or problems
- Check existing discussions and documentation first.