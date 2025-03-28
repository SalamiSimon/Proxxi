# This is a sample dynamic code file for modifying subscription responses
# The variable 'response_data' is automatically provided and should be modified in-place

# Check if subscription exists and modify its properties
if "subscription" in response_data:
    # Set trial to a far-future date
    response_data["subscription"]["trialEndsAt"] = "2199-03-22T17:33:04Z"
    
    # Set subscription end to a far-future date
    response_data["subscription"]["endsAt"] = "2199-03-22T17:33:04Z"
    
    # Set subscription state to active
    response_data["subscription"]["state"] = "active"
    
    # If there's a 'plan' object, modify it too
    if "plan" in response_data["subscription"]:
        response_data["subscription"]["plan"]["name"] = "Premium"
else:
    # Create subscription object if it doesn't exist
    response_data["subscription"] = {
        "trialEndsAt": "2099-03-22T17:33:04Z",
        "endsAt": "2099-03-22T17:33:04Z",
        "state": "active",
        "plan": {
            "name": "Premium"
        }
    }

# You can also add completely new fields to the response
response_data["modified_by"] = "MITM Modular" 