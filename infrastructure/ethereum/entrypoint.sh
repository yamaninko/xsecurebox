#!/bin/sh
set -eu
INDEX="${NODE_INDEX:-1}"
NAME="securebox-eth-${INDEX}"
DATADIR=/data

if [ ! -d "${DATADIR}/geth" ]; then
  geth --datadir "${DATADIR}" init /ethereum/genesis.json
fi
if [ ! -d "${DATADIR}/keystore" ] || [ -z "$(ls -A "${DATADIR}/keystore" 2>/dev/null || true)" ]; then
  geth --datadir "${DATADIR}" account import --password /ethereum/password.txt /ethereum/sealer.hex || true
fi

NODEKEY="/ethereum/nodekey-${INDEX}"
IP="$(hostname -i | awk '{print $1}')"
EXTRA="--nodekey ${NODEKEY} --nat extip:${IP}"
EXTRA="${EXTRA} --unlock 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
EXTRA="${EXTRA} --password /ethereum/password.txt --allow-insecure-unlock"

if [ "${INDEX}" = "1" ]; then
  EXTRA="${EXTRA} --mine --miner.etherbase 0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
fi
if [ -n "${BOOTNODE:-}" ]; then
  EXTRA="${EXTRA} --bootnodes ${BOOTNODE}"
fi

exec geth --datadir "${DATADIR}" \
  --networkid 4242 \
  --http --http.addr 0.0.0.0 --http.port 8545 \
  --http.api eth,net,web3,admin,clique \
  --http.vhosts '*' --http.corsdomain '*' \
  --port 30303 \
  --ipcdisable \
  --syncmode full \
  --verbosity 3 \
  ${EXTRA}
