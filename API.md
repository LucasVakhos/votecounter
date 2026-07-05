# Rhymers REST API Documentation

## Overview

The Rhymers REST API provides endpoints for contest management, vote processing, and results generation.

**Base URL:** `https://localhost:7070/api` (development)
**API Version:** v1

## Swagger Documentation

Interactive API documentation is available at:
- Development: `https://localhost:7070` (Swagger UI)
- Production: Available via `/swagger/v1/swagger.json`

## Endpoints

### Contests

#### Get All Contests
```
GET /api/contests
```

**Response (200 OK):**
```json
[
  {
    "id": "a1b2c3d4e5f6",
    "number": "001",
    "name": "Poetry Contest 2026",
    "hostName": "John Doe",
    "works": [
      {
        "number": 1,
        "title": "My Poem",
        "author": "Jane Smith",
        "topic": "Love",
        "content": "..."
      }
    ]
  }
]
```

#### Get Contest by ID
```
GET /api/contests/{id}
```

**Parameters:**
- `id` (string, required): Contest ID (GUID format)

**Response (200 OK):** Contest object

#### Create Contest
```
POST /api/contests
Content-Type: application/json
```

**Request Body:**
```json
{
  "name": "Poetry Contest 2026",
  "hostName": "John Doe"
}
```

**Response (201 Created):**
```json
{
  "id": "a1b2c3d4e5f6",
  "number": "001",
  "name": "Poetry Contest 2026",
  "hostName": "John Doe"
}
```

#### Update Contest
```
PUT /api/contests/{id}
Content-Type: application/json
```

**Request Body:**
```json
{
  "name": "Updated Contest Name",
  "hostName": "New Host"
}
```

#### Add Work to Contest
```
POST /api/contests/{id}/works
Content-Type: application/json
```

**Request Body:**
```json
{
  "number": 1,
  "title": "My Poem",
  "author": "Jane Smith",
  "topic": "Love",
  "content": "Beautiful words..."
}
```

#### Get Contest Works
```
GET /api/contests/{id}/works
```

**Response (200 OK):**
```json
[
  {
    "number": 1,
    "title": "My Poem",
    "author": "Jane Smith"
  }
]
```

### Votes

#### Import Votes
```
POST /api/votes/import
Content-Type: application/json
```

**Request Body:**
```json
{
  "contestId": "a1b2c3d4e5f6",
  "voteText": "John Doe\n01-4\n02-3\n03-5"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "blocks": [
    {
      "voterName": "John Doe",
      "entries": [
        {
          "workNumber": 1,
          "score": 4
        }
      ]
    }
  ]
}
```

#### Validate Votes
```
POST /api/votes/validate
Content-Type: application/json
```

**Request Body:**
```json
{
  "contestId": "a1b2c3d4e5f6",
  "importResult": { ... }
}
```

**Response (200 OK):** Validated import result with errors/warnings

#### Get Contest Votes
```
GET /api/votes/contest/{contestId}
```

**Response (200 OK):** Array of vote entries

#### Generate Results
```
POST /api/votes/results
Content-Type: application/json
```

**Request Body:**
```json
{
  "contestId": "a1b2c3d4e5f6"
}
```

**Response (200 OK):**
```json
{
  "rows": [
    {
      "placeNo": 1,
      "workNo": 3,
      "title": "My Poem",
      "author": "Jane Smith",
      "rate": 15.5,
      "acceptedVotes": 3,
      "average": 5.17
    }
  ],
  "workCount": 5,
  "voterCount": 3,
  "acceptedVoteCount": 15
}
```

### Health & Info

#### Health Check
```
GET /api/health
```

**Response (200 OK):**
```json
{
  "status": "healthy",
  "timestamp": "2026-07-04T12:00:00Z"
}
```

#### API Version
```
GET /api/version
```

**Response (200 OK):**
```json
{
  "version": "1.0.0",
  "name": "Rhymers API",
  "timestamp": "2026-07-04T12:00:00Z"
}
```

## Error Responses

### 400 Bad Request
```json
{
  "error": "Contest ID is required"
}
```

### 404 Not Found
```json
{
  "error": "Contest with ID xyz not found"
}
```

### 500 Internal Server Error
```json
{
  "error": "An error occurred processing votes"
}
```

## Running the API

```bash
# Start the API server
dotnet run --project src/Rhymers.Api/Rhymers.Api.csproj

# The server will start on:
# https://localhost:7070 (production)
# http://localhost:5070  (development)
```

## Integration Examples

### Python
```python
import requests

BASE_URL = "http://localhost:5070/api"

# Create contest
response = requests.post(
    f"{BASE_URL}/contests",
    json={"name": "My Contest", "hostName": "Admin"}
)
contest = response.json()
contest_id = contest["id"]

# Import votes
response = requests.post(
    f"{BASE_URL}/votes/import",
    json={
        "contestId": contest_id,
        "voteText": "Voter1\n01-5\n02-4"
    }
)
```

### JavaScript/Fetch
```javascript
const BASE_URL = "http://localhost:5070/api";

// Get all contests
const response = await fetch(`${BASE_URL}/contests`);
const contests = await response.json();

// Create contest
const newContest = await fetch(`${BASE_URL}/contests`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    name: "My Contest",
    hostName: "Admin"
  })
});
```

### cURL
```bash
# Get all contests
curl https://localhost:7070/api/contests

# Create contest
curl -X POST https://localhost:7070/api/contests \
  -H "Content-Type: application/json" \
  -d '{"name":"My Contest","hostName":"Admin"}'

# Import votes
curl -X POST https://localhost:7070/api/votes/import \
  -H "Content-Type: application/json" \
  -d '{"contestId":"a1b2c3d4e5f6","voteText":"Voter1\n01-5\n02-4"}'
```
