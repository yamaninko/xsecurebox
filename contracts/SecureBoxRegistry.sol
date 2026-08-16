// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// @title XSecureBox on-chain integrity registry
/// @notice Stores ciphertext commitments. Never stores plaintext or KEKs.
contract SecureBoxRegistry {
    bytes32 public immutable systemId;
    address public owner;
    bool public paused;

    struct Record {
        bytes32 payloadHash;
        bytes32 algorithmId;
        address registrar;
        uint64 registeredAt;
        bool revoked;
    }

    mapping(bytes32 => Record) public records;

    event Registered(bytes32 indexed keyId, bytes32 payloadHash, bytes32 algorithmId, bytes32 systemId);
    event Revoked(bytes32 indexed keyId);

    modifier onlyOwner() {
        require(msg.sender == owner, "not owner");
        _;
    }

    constructor(bytes32 _systemId) {
        require(_systemId != bytes32(0), "systemId");
        systemId = _systemId;
        owner = msg.sender;
    }

    function setPaused(bool value) external onlyOwner {
        paused = value;
    }

    function transferOwnership(address newOwner) external onlyOwner {
        require(newOwner != address(0), "owner");
        owner = newOwner;
    }

    function register(bytes32 keyId, bytes32 payloadHash, bytes32 algorithmId) external onlyOwner {
        require(!paused, "paused");
        require(keyId != bytes32(0), "keyId");
        require(payloadHash != bytes32(0), "hash");
        require(records[keyId].registeredAt == 0, "exists");
        records[keyId] = Record({
            payloadHash: payloadHash,
            algorithmId: algorithmId,
            registrar: msg.sender,
            registeredAt: uint64(block.timestamp),
            revoked: false
        });
        emit Registered(keyId, payloadHash, algorithmId, systemId);
    }

    function verify(bytes32 keyId, bytes32 payloadHash) external view returns (bool ok) {
        Record memory rec = records[keyId];
        return !paused && rec.registeredAt != 0 && !rec.revoked && rec.payloadHash == payloadHash;
    }

    function revoke(bytes32 keyId) external onlyOwner {
        require(records[keyId].registeredAt != 0, "missing");
        records[keyId].revoked = true;
        emit Revoked(keyId);
    }
}
