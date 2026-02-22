#!/usr/bin/env bash
set -u

API_URL="${API_URL:-http://localhost:5080}"
TENANT="clinic1"
COOKIE_JAR="${TMPDIR:-/tmp}/vitaent-smoke-auth-cookie-$$.txt"
AUTH_HEADER_FILE="${TMPDIR:-/tmp}/vitaent-smoke-auth-headers-$$.txt"
BODY_FILE="${TMPDIR:-/tmp}/vitaent-smoke-auth-body-$$.txt"

cleanup() {
  rm -f "$COOKIE_JAR" "$AUTH_HEADER_FILE" "$BODY_FILE"
}
trap cleanup EXIT

if ! command -v curl >/dev/null 2>&1; then
  echo "FAIL: curl is required but not installed."
  exit 1
fi

access_token=""
doctor_id=""
appointment_id=""

iso_utc_now_plus_minutes() {
  local minutes="$1"

  if date -u -d "+${minutes} minutes" +"%Y-%m-%dT%H:%M:%SZ" >/dev/null 2>&1; then
    date -u -d "+${minutes} minutes" +"%Y-%m-%dT%H:%M:%SZ"
    return 0
  fi

  if date -u -v+"${minutes}"M +"%Y-%m-%dT%H:%M:%SZ" >/dev/null 2>&1; then
    date -u -v+"${minutes}"M +"%Y-%m-%dT%H:%M:%SZ"
    return 0
  fi

  return 1
}

pass() {
  echo "PASS: $1"
}

fail() {
  echo "FAIL: $1"
  if [ -s "$BODY_FILE" ]; then
    echo "--- response body ---"
    cat "$BODY_FILE"
    echo "---------------------"
  fi
  exit 1
}

run_request() {
  local method="$1"
  local url="$2"
  shift 2

  : >"$AUTH_HEADER_FILE"
  : >"$BODY_FILE"

  local status
  status=$(curl -sS -X "$method" "$url" \
    -D "$AUTH_HEADER_FILE" \
    -o "$BODY_FILE" \
    "$@" \
    -w "%{http_code}") || return 1

  echo "$status"
}

echo "API_URL=$API_URL"
echo "Using cookie jar: $COOKIE_JAR"

status=$(run_request GET "$API_URL/health") || fail "Step 1 - GET /health request failed"
[ "$status" = "200" ] || fail "Step 1 - GET /health expected HTTP 200, got $status"
pass "Step 1 - GET /health"

status=$(run_request GET "$API_URL/api/tenant/me?tenant=$TENANT") || fail "Step 2 - GET /api/tenant/me request failed"
[ "$status" = "200" ] || fail "Step 2 - GET /api/tenant/me expected HTTP 200, got $status"
pass "Step 2 - GET /api/tenant/me?tenant=$TENANT"

status=$(run_request POST "$API_URL/api/auth/sign-in?tenant=$TENANT" \
  -c "$COOKIE_JAR" \
  -H "Content-Type: application/json" \
  --data '{"email":"admin@clinic1.local","password":"Admin123!"}') || fail "Step 3 - POST /api/auth/sign-in request failed"
[ "$status" = "200" ] || fail "Step 3 - POST /api/auth/sign-in expected HTTP 200, got $status"

access_token=$(sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p' "$BODY_FILE" | head -n 1)
[ -n "$access_token" ] || fail "Step 3 - accessToken not found in sign-in response"
pass "Step 3 - POST /api/auth/sign-in?tenant=$TENANT"

status=$(run_request GET "$API_URL/api/tenant/me?tenant=$TENANT" \
  -H "Authorization: Bearer $access_token") || fail "Step 4 - GET /api/tenant/me with bearer request failed"
[ "$status" = "200" ] || fail "Step 4 - GET /api/tenant/me with bearer expected HTTP 200, got $status"
pass "Step 4 - GET /api/tenant/me?tenant=$TENANT with bearer"

status=$(run_request POST "$API_URL/api/auth/refresh?tenant=$TENANT" \
  -b "$COOKIE_JAR" \
  -c "$COOKIE_JAR") || fail "Step 5 - POST /api/auth/refresh request failed"
[ "$status" = "200" ] || fail "Step 5 - POST /api/auth/refresh expected HTTP 200, got $status"
pass "Step 5 - POST /api/auth/refresh?tenant=$TENANT"

status=$(run_request GET "$API_URL/api/doctors?tenant=$TENANT" \
  -H "Authorization: Bearer $access_token") || fail "Step 6 - GET /api/doctors request failed"
[ "$status" = "200" ] || fail "Step 6 - GET /api/doctors expected HTTP 200, got $status"
doctor_id=$(sed -n 's/.*"id":"\([^"]*\)".*/\1/p' "$BODY_FILE" | head -n 1)
[ -n "$doctor_id" ] || fail "Step 6 - could not extract doctor id from /api/doctors response"
pass "Step 6 - GET /api/doctors?tenant=$TENANT"

starts_at=$(iso_utc_now_plus_minutes 30) || fail "Step 7 - could not compute startsAt timestamp"
ends_at=$(iso_utc_now_plus_minutes 60) || fail "Step 7 - could not compute endsAt timestamp"
status=$(run_request POST "$API_URL/api/appointments?tenant=$TENANT" \
  -H "Authorization: Bearer $access_token" \
  -H "Content-Type: application/json" \
  --data "{\"doctorId\":\"$doctor_id\",\"patientName\":\"Smoke Test\",\"startsAt\":\"$starts_at\",\"endsAt\":\"$ends_at\"}") || fail "Step 7 - POST /api/appointments request failed"
[ "$status" = "201" ] || fail "Step 7 - POST /api/appointments expected HTTP 201, got $status"
appointment_id=$(tr -d '\r' < "$AUTH_HEADER_FILE" | sed -n 's#^Location: /api/appointments/\([0-9a-fA-F-]\{36\}\)$#\1#p' | head -n 1)
if [ -z "$appointment_id" ]; then
  appointment_id=$(sed -n 's/.*"id":"\([0-9a-fA-F-]\{36\}\)".*/\1/p' "$BODY_FILE" | head -n 1)
fi
[ -n "$appointment_id" ] || fail "Step 7 - could not extract appointment id from Location header or response body"
pass "Step 7 - POST /api/appointments?tenant=$TENANT"

status=$(run_request PATCH "$API_URL/api/appointments/$appointment_id/status?tenant=$TENANT" \
  -H "Authorization: Bearer $access_token" \
  -H "Content-Type: application/json" \
  --data '{"status":"Confirmed"}') || fail "Step 8 - PATCH confirm appointment request failed"
[ "$status" = "200" ] || fail "Step 8 - PATCH confirm appointment expected HTTP 200, got $status"
pass "Step 8 - PATCH /api/appointments/{id}/status Confirmed"

status=$(run_request PATCH "$API_URL/api/appointments/$appointment_id/status?tenant=$TENANT" \
  -H "Authorization: Bearer $access_token" \
  -H "Content-Type: application/json" \
  --data '{"status":"Cancelled"}') || fail "Step 9 - PATCH cancel appointment request failed"
[ "$status" = "200" ] || fail "Step 9 - PATCH cancel appointment expected HTTP 200, got $status"
pass "Step 9 - PATCH /api/appointments/{id}/status Cancelled"

status=$(run_request PATCH "$API_URL/api/appointments/$appointment_id/status?tenant=$TENANT" \
  -H "Authorization: Bearer $access_token" \
  -H "Content-Type: application/json" \
  --data '{"status":"Cancelled"}') || fail "Step 10 - PATCH cancel-again appointment request failed"
[ "$status" = "409" ] || fail "Step 10 - PATCH cancel-again appointment expected HTTP 409, got $status"
pass "Step 10 - PATCH /api/appointments/{id}/status Cancelled again (409)"

status=$(run_request POST "$API_URL/api/auth/sign-out?tenant=$TENANT" \
  -b "$COOKIE_JAR") || fail "Step 11 - POST /api/auth/sign-out request failed"
[ "$status" = "204" ] || fail "Step 11 - POST /api/auth/sign-out expected HTTP 204, got $status"
pass "Step 11 - POST /api/auth/sign-out?tenant=$TENANT"

echo "Smoke auth + appointment status flow completed successfully."
