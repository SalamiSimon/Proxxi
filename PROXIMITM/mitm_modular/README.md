# Modular MITM Response Modifier

A modular Man-in-the-Middle proxy system that allows dynamic and static modification of JSON API responses based on configurable targets.

## Features

- Target specific URLs with exact match, substring match, or regex patterns
- Optionally filter by HTTP status code
- Ability to change HTTP status codes (e.g., change 404 to 200)
- Two types of modifications:
  - **Dynamic**: Use custom Python code to modify the JSON response
  - **Static**: Replace the response with a predefined JSON payload
- Enable/disable targets without removing them
- Command-line interface for managing targets
- Database storage of targets for persistence

## Requirements

- Python 3.6+
- mitmproxy
- tabulate (for CLI formatting)

## Installation

1. Install dependencies:
```
pip install mitmproxy tabulate
```

2. Clone or copy this directory to your project

## Usage

### Starting the MITM Proxy

Start mitmproxy with the addon script:

```
mitmproxy -s mitm_modular/mitm_core.py
```

or for the web interface:

```
mitmweb -s mitm_modular/mitm_core.py
```

### Managing Targets with CLI

The CLI tool allows you to manage targets without stopping the proxy.

#### Listing all targets

```
python -m mitm_modular.cli list
```

#### Adding a dynamic modification target

```
python -m mitm_modular.cli add "https://api.example.com/subscriptions" --type dynamic --code "response_data['subscription']['state'] = 'active'"
```

Or with a file containing the Python code:

```
python -m mitm_modular.cli add "https://api.example.com/subscriptions" --type dynamic --code-file my_code.py
```

#### Adding a static response target

```
python -m mitm_modular.cli add "https://api.example.com/status" --type static --response '{"status": "premium", "expiresAt": "2099-12-31"}'
```

Or with a file containing the JSON:

```
python -m mitm_modular.cli add "https://api.example.com/status" --type static --response-file my_response.json
```

#### Changing HTTP status codes

To match a specific status code but also change it (e.g., match 404 responses and change them to 200):

```
python -m mitm_modular.cli add "https://api.example.com/status" --status 404 --target-status 200 --type static --response '{"status": "success", "message": "Resource found"}'
```

To change any response to a specific status code regardless of its original status:

```
python -m mitm_modular.cli add "https://api.example.com/status" --target-status 200 --type static --response '{"status": "success"}'
```

#### Viewing target details

```
python -m mitm_modular.cli view 1
```

#### Enabling/Disabling targets

```
python -m mitm_modular.cli disable 1
python -m mitm_modular.cli enable 1
```

#### Deleting targets

```
python -m mitm_modular.cli delete 1
```

## Example Dynamic Code

Here's an example of dynamic code that modifies subscription information:

```python
if "subscription" in response_data:
    response_data["subscription"]["trialEndsAt"] = "2199-03-22T17:33:04Z"
    response_data["subscription"]["endsAt"] = "2199-03-22T17:33:04Z"
    response_data["subscription"]["state"] = "active"
else:
    # Create subscription object if it doesn't exist
    response_data["subscription"] = {
        "trialEndsAt": "2099-03-22T17:33:04Z",
        "state": "active"
    }
```

## Creating a UI Application

This modular system is designed to be easily integrated with a UI application. The UI would need to:

1. Provide a form to enter target details (URL, match status code)
2. Allow selection of modification type (dynamic/static)
3. Allow specifying a target status code to change responses to
4. Provide a code editor for dynamic modifications
5. Provide a JSON editor for static responses
6. Interface with the `TargetDatabase` class to store entries

The database schema is simple and can be accessed directly using the `TargetDatabase` class from the `mitm_modular.database` module.

## Architecture Overview

- **database.py**: Handles storage and retrieval of target definitions
- **mitm_core.py**: Contains the mitmproxy addon and response modification logic
- **cli.py**: Command-line interface for managing targets

## License

This project is open source and available under the MIT License. 