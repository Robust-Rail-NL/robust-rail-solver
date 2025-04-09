# Use Ubuntu 20.04 base image
FROM  --platform=linux/amd64 ubuntu:20.04

# Set non-interactive mode to avoid prompts during package installation
ENV DEBIAN_FRONTEND=noninteractive

# Update package list and install dependencies
RUN apt-get update && apt-get install -y \
    wget \
    curl \
    git \
    build-essential \
    apt-transport-https \
    ca-certificates \
    gnupg \
    software-properties-common

# Install Microsoft package repository for .NET
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN chmod 755 packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN rm packages-microsoft-prod.deb


# Install .NET SDK 8.0
RUN apt-get update && apt-get install -y dotnet-sdk-8.0

# Verify installation
RUN dotnet --version

# Set working directory
WORKDIR /workspace

# Set default command to keep the container running
CMD ["bash"]
