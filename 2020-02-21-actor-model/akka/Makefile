SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
.SHELLFLAGS := -eu -o pipefail -c
MAKEFLAGS += --warn-undefined-variables 
MAKEFLAGS += --no-builtin-rules

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
else
	BUILD_NUMBER := $(shell date +"%Y.%m.%d.%H%M%S")
	DETECTED_OS := $(shell sh -c 'uname 2>/dev/null || echo Unknown')
endif

.PHONY: run.eventstore
run.eventstore:
ifeq ($(DETECTED_OS), Windows)
	if not exist $(CURDIR)\.data mkdir $(CURDIR)\.data
	if not exist $(CURDIR)\.data\eventstore mkdir $(CURDIR)\.data\eventstore
else
	mkdir -p $(CURDIR)/.data/eventstore
endif
	docker run --rm -v $(CURDIR)/.data/eventstore:/var/lib/eventstore -p 2113:2113 -p 1113:1113 eventstore/eventstore:22.10.1-buster-slim --insecure --run-projections=All --enable-external-tcp --enable-atom-pub-over-http