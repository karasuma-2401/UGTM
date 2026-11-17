#!/bin/sh
set -eu

# Explicit provisioning only; existing objects are never removed.
if [ "$S3_ACCESS_KEY" = "$AI_S3_ACCESS_KEY" ] ||
   [ "$S3_ACCESS_KEY" = "$MINIO_ROOT_USER" ] ||
   [ "$AI_S3_ACCESS_KEY" = "$MINIO_ROOT_USER" ] ||
   [ "$S3_BUCKET_NAME" = "$AI_S3_BUCKET_NAME" ]; then
  echo 'Use separate administrator, UGTM and AI users, and separate buckets.' >&2
  exit 1
fi
mc alias set production http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null

provision() {
  bucket=$1
  user=$2
  password=$3
  policy=$4
  case "$bucket" in ''|*[!a-z0-9.-]*) echo 'Invalid bucket name.' >&2; exit 1;; esac
  mc mb --ignore-existing "production/$bucket"
  mc anonymous set none "production/$bucket"
  mc admin user add production "$user" "$password"
  cat > /tmp/policy.json <<EOF
{"Version":"2012-10-17","Statement":[
  {"Effect":"Allow","Action":["s3:ListBucket","s3:GetBucketLocation","s3:ListBucketMultipartUploads"],"Resource":["arn:aws:s3:::$bucket"]},
  {"Effect":"Allow","Action":["s3:GetObject","s3:PutObject","s3:DeleteObject","s3:AbortMultipartUpload","s3:ListMultipartUploadParts"],"Resource":["arn:aws:s3:::$bucket/*"]}
]}
EOF
  mc admin policy create production "$policy" /tmp/policy.json
  mc admin policy attach production "$policy" --user "$user"
}
provision "$S3_BUCKET_NAME" "$S3_ACCESS_KEY" "$S3_SECRET_KEY" ugtm-storage
provision "$AI_S3_BUCKET_NAME" "$AI_S3_ACCESS_KEY" "$AI_S3_SECRET_KEY" ugtm-ai-storage
