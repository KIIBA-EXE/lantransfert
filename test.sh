#!/bin/bash
# Test script for LAN Transfer
# Creates a test file and checks if the application can be started

set -e

TEST_DIR="/tmp/lantransfer_test"
TEST_FILE="$TEST_DIR/test_file.txt"

echo "🔬 LAN Transfer Test Script"
echo "==========================="

# Create test directory
mkdir -p "$TEST_DIR"

# Create a test file
echo "This is a test file for LAN Transfer." > "$TEST_FILE"
echo "Generated at: $(date)" >> "$TEST_FILE"

# Generate a larger test file (1MB)
dd if=/dev/urandom of="$TEST_DIR/large_test_1mb.bin" bs=1024 count=1024 2>/dev/null

echo "✅ Created test files in $TEST_DIR"
ls -la "$TEST_DIR"

echo ""
echo "📋 Test Instructions:"
echo "1. Open two terminals"
echo "2. Run: cd $(pwd) && dotnet run --project src/LanTransfer.Desktop"
echo "3. On machine 2 (or another terminal), run the same command"
echo "4. Wait for both to detect each other"
echo "5. Drag $TEST_FILE to the drop zone and send to the other machine"
echo "6. Accept or reject the transfer on the receiving machine"
echo ""
echo "🎯 Expected Results:"
echo "- Both apps should see each other in the peer list"
echo "- Sender should see progress bar during transfer"
echo "- Receiver should see accept/reject dialog"
echo "- File should appear in ~/Downloads/LanTransfer/ after acceptance"
