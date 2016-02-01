DESTDIR?=/usr/local
DESTDIR2=$(shell realpath $(DESTDIR))
MONO_PATH?=/usr/bin

EX_NUGET:=nuget/bin/nuget

XBUILD?=$(MONO_PATH)/xbuild
MONO?=$(MONO_PATH)/mono
GIT?=$(shell which git)

NUGET?=$(EX_NUGET)

all: binary ;

binary: nuget-packages-restore 
	$(XBUILD) *.sln /p:Configuration=Release

# External tools

external-tools: nuget ;

nuget: $(NUGET) ;

submodule:
	$(GIT) submodule update --init --recursive

$(EX_NUGET): submodule
	cd nuget && $(MAKE)

# NuGet

nuget-packages-restore: external-tools
	$(NUGET) restore *.sln

# Install

install: binary 
	mkdir -p $(DESTDIR2)/lib/tweetdeleter $(DESTDIR2)/bin
	cp bin/Release/* $(DESTDIR2)/lib/tweetdeleter/
	echo "#!/bin/sh" > $(DESTDIR2)/bin/tweetdeleter
	echo "mono $(DESTDIR2)/lib/tweetdeleter/tweetdeleter.exe \$$*" >> $(DESTDIR2)/bin/tweetdeleter
	chmod +x $(DESTDIR2)/bin/tweetdeleter

# Clean

clean:
	$(RM) -rf obj

