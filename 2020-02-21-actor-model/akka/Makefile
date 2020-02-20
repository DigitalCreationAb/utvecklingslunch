SHELL := /bin/bash
.DEFAULT_GOAL := run.eventstore
.PHONY: run.eventstore

run.eventstore:
	mkdir -p ./.data
	if [ ! -d "./.data/EventStore-OSS-Linux-Ubuntu-18.04-v5.0.0" ]; then \
		wget https://eventstore.org/downloads/ubuntu/EventStore-OSS-Linux-Ubuntu-18.04-v5.0.0.tar.gz -O ./.data/eventstore.tar.gz && tar -xvzf ./.data/eventstore.tar.gz -C ./.data && rm ./.data/eventstore.tar.gz; \
	fi
	cd ./.data/EventStore-OSS-Linux-Ubuntu-18.04-v5.0.0 && ./run-node.sh --start-standard-projections=True --run-projections=All