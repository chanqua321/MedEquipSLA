# Gateway

YARP reverse proxy; no business DbContext or business rules. Run from root: `dotnet run --project src/Gateway/MedEquipSLA.Gateway --launch-profile Gateway`, or `docker compose up -d --build gateway`. Port 5100. Routes /identity/*, /equipment/*, /maintenance/*, /incident/*, /notification/* remove the prefix. APIs validate JWT themselves. SignalR route can pass through when Notification hub is implemented. See root README for layer mapping, team Git workflow and PRN232 matrix. Gateway is not the independent gRPC service; Equipment owns that endpoint.
