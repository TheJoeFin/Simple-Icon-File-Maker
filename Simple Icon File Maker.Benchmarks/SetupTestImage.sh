#!/bin/bash

echo "================================================================"
echo "  PreviewStack Benchmark - Test Image Setup"
echo "================================================================"
echo ""

BUILD_DIR="bin/Release/net9.0-windows10.0.22621"
TARGET_FILE="$BUILD_DIR/Perf.png"

if [ ! -d "$BUILD_DIR" ]; then
    echo "Creating build directory..."
    mkdir -p "$BUILD_DIR"
fi

echo "This script helps you set up a test image for the benchmarks."
echo ""
echo "The test image should be:"
echo "  - A PNG file"
echo "  - Recommended size: 512x512 pixels or larger"
echo "  - Named: Perf.png"
echo ""
echo "Options:"
echo "  1. Specify path to an existing PNG image"
echo "  2. I'll manually copy it later"
echo "  3. Exit"
echo ""

read -p "Select an option (1-3): " choice

case $choice in
    1)
        echo ""
        read -p "Enter the full path to your PNG image: " SOURCE_FILE
        
        if [ ! -f "$SOURCE_FILE" ]; then
   echo ""
          echo "Error: File not found: $SOURCE_FILE"
   echo ""
   exit 1
    fi
        
        # Check if it's a PNG file
        if [[ ! "$SOURCE_FILE" =~ \.png$ ]]; then
       echo ""
         echo "Warning: This doesn't appear to be a PNG file."
          echo "The benchmarks expect a PNG image."
         read -p "Continue anyway? (y/n): " continue_choice
       if [[ ! "$continue_choice" =~ ^[Yy]$ ]]; then
         exit 0
          fi
     fi
        
echo ""
 echo "Copying $SOURCE_FILE"
        echo "     to $TARGET_FILE..."

        cp "$SOURCE_FILE" "$TARGET_FILE"
        
        if [ -f "$TARGET_FILE" ]; then
    echo ""
            echo "? Success! Test image installed."
            echo ""
        echo "You can now run the benchmarks with:"
            echo "  dotnet run -c Release"
    echo ""
        else
            echo ""
       echo "? Failed to copy the file."
   echo ""
        fi
        ;;
    
    2)
        echo ""
  echo "Manual Setup Instructions:"
        echo "?????????????????????????????????????????"
        echo ""
        echo "1. Choose a PNG image (512x512 or larger recommended)"
      echo "2. Rename it to: Perf.png"
        echo "3. Copy it to: $BUILD_DIR/"
  echo ""
      echo "Full path: $(pwd)/$TARGET_FILE"
      echo ""
        echo "After copying, run:"
        echo "  dotnet run -c Release"
        echo ""
 ;;
 
    3)
   echo "Exiting..."
        exit 0
        ;;
    
    *)
        echo "Invalid option. Exiting..."
        exit 1
        ;;
esac
