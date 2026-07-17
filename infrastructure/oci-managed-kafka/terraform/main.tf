locals {
  replication_factor = var.broker_node_count >= 3 ? "3" : "1"
  min_isr            = var.broker_node_count >= 3 ? "2" : "1"
  create_sasl_secret = var.enable_sasl_scram_superuser && var.sasl_scram_secret_id == "" && var.create_sasl_scram_secret

  cluster_properties = merge(
    {
      "allow.everyone.if.no.acl.found"           = tostring(var.allow_everyone_if_no_acl_found)
      "auto.create.topics.enable"                = tostring(var.auto_create_topics_enable)
      "default.replication.factor"               = local.replication_factor
      "min.insync.replicas"                      = local.min_isr
      "offsets.topic.replication.factor"         = local.replication_factor
      "transaction.state.log.min.isr"            = local.min_isr
      "transaction.state.log.replication.factor" = local.replication_factor
      "message.max.bytes"                        = tostring(var.message_max_bytes)
      "compression.type"                         = "producer"
    },
    var.extra_cluster_properties
  )

  sasl_scram_secret_compartment_id = var.sasl_scram_secret_compartment_id != "" ? var.sasl_scram_secret_compartment_id : var.compartment_id
  sasl_scram_secret_id             = var.sasl_scram_secret_id != "" ? var.sasl_scram_secret_id : try(oci_vault_secret.kafka_superuser[0].id, null)
}

resource "oci_identity_policy" "managed_kafka_service" {
  compartment_id = var.compartment_id
  name           = "${replace(var.display_name, "-", "_")}_rawfka"
  description    = "Allows OCI Streaming with Apache Kafka service to create network endpoints and update the PoC SASL/SCRAM secret."
  freeform_tags  = var.freeform_tags

  statements = [
    "allow service rawfka to use vnics in tenancy",
    "allow service rawfka to use subnets in tenancy",
    "allow service rawfka to use network-security-groups in tenancy",
    "allow service rawfka to read secrets in tenancy",
    "allow service rawfka to {SECRET_UPDATE} in tenancy",
    "allow service rawfka to use secrets in tenancy where request.operation = 'UpdateSecret'",
  ]
}

resource "oci_kms_vault" "kafka_poc" {
  count = local.create_sasl_secret ? 1 : 0

  compartment_id = local.sasl_scram_secret_compartment_id
  display_name   = var.sasl_scram_vault_name
  vault_type     = "DEFAULT"
  freeform_tags  = var.freeform_tags

  timeouts {
    create = "30m"
    update = "30m"
    delete = "30m"
  }
}

resource "oci_kms_key" "kafka_poc" {
  count = local.create_sasl_secret ? 1 : 0

  compartment_id      = local.sasl_scram_secret_compartment_id
  display_name        = var.sasl_scram_key_name
  management_endpoint = oci_kms_vault.kafka_poc[0].management_endpoint
  protection_mode     = "SOFTWARE"
  freeform_tags       = var.freeform_tags

  key_shape {
    algorithm = "AES"
    length    = 32
  }

  timeouts {
    create = "30m"
    update = "30m"
    delete = "30m"
  }
}

resource "oci_vault_secret" "kafka_superuser" {
  count = local.create_sasl_secret ? 1 : 0

  compartment_id = local.sasl_scram_secret_compartment_id
  secret_name    = var.sasl_scram_secret_name
  description    = "Generated SASL/SCRAM superuser password for ${var.display_name}."
  vault_id       = oci_kms_vault.kafka_poc[0].id
  key_id         = oci_kms_key.kafka_poc[0].id
  freeform_tags  = var.freeform_tags

  secret_content {
    content_type = "BASE64"
    content      = base64encode("placeholder")
    name         = "initial"
    stage        = "CURRENT"
  }

  lifecycle {
    ignore_changes = [secret_content]
  }

  timeouts {
    create = "30m"
    update = "30m"
    delete = "30m"
  }
}

resource "oci_managed_kafka_kafka_cluster_config" "poc" {
  compartment_id = var.compartment_id
  display_name   = "${var.display_name}-config"
  freeform_tags  = var.freeform_tags

  latest_config {
    properties = local.cluster_properties
  }
}

resource "oci_managed_kafka_kafka_cluster" "poc" {
  compartment_id         = var.compartment_id
  display_name           = var.display_name
  cluster_type           = var.cluster_type
  kafka_version          = var.kafka_version
  coordination_type      = var.coordination_type
  cluster_config_id      = oci_managed_kafka_kafka_cluster_config.poc.id
  cluster_config_version = oci_managed_kafka_kafka_cluster_config.poc.latest_config[0].version_number
  freeform_tags          = var.freeform_tags

  access_subnets {
    subnets = var.subnet_ocids
  }

  broker_shape {
    node_count          = var.broker_node_count
    ocpu_count          = var.broker_ocpu_count
    node_shape          = var.broker_node_shape
    storage_size_in_gbs = var.broker_storage_size_in_gbs
  }

  timeouts {
    create = "60m"
    update = "60m"
    delete = "60m"
  }

  depends_on = [
    oci_identity_policy.managed_kafka_service,
  ]
}

resource "oci_managed_kafka_kafka_cluster_superusers_management" "poc" {
  count = var.enable_sasl_scram_superuser ? 1 : 0

  kafka_cluster_id = oci_managed_kafka_kafka_cluster.poc.id
  enable_superuser = true
  compartment_id   = local.sasl_scram_secret_compartment_id
  secret_id        = local.sasl_scram_secret_id

  timeouts {
    create = "30m"
    update = "30m"
    delete = "30m"
  }

  depends_on = [
    oci_identity_policy.managed_kafka_service,
  ]
}
