pipeline {
  agent any

  options {
    timestamps()
    disableConcurrentBuilds()
  }

  environment {
    DOCKERHUB_REGISTRY = 'docker.io'

    K8S_NAMESPACE = 'voting-app'
    K8S_MANIFEST_DIR = 'k8s/manifests'

    VOTE_IMAGE = 'hiiamgay/vote'
    RESULT_IMAGE = 'hiiamgay/result'
    WORKER_IMAGE = 'hiiamgay/worker'
  }

  stages {
    stage('Checkout') {
      steps {
        checkout scm
        script {
          env.IMAGE_TAG = sh(script: 'git rev-parse --short=7 HEAD', returnStdout: true).trim()
        }
      }
    }

    stage('Docker login') {
      steps {
        withCredentials([usernamePassword(
          credentialsId: 'dockerhub-creds',
          usernameVariable: 'DOCKERHUB_USER',
          passwordVariable: 'DOCKERHUB_PASS'
        )]) {
          sh '''
            set -eu
            echo "$DOCKERHUB_PASS" | docker login "$DOCKERHUB_REGISTRY" -u "$DOCKERHUB_USER" --password-stdin
          '''
        }
      }
    }

    stage('Build') {
      steps {
        sh '''
          set -eu
          docker build -t "$VOTE_IMAGE:$IMAGE_TAG" ./vote
          docker build -t "$RESULT_IMAGE:$IMAGE_TAG" ./result
          docker build -t "$WORKER_IMAGE:$IMAGE_TAG" ./worker
        '''
      }
    }

    stage('Push') {
      steps {
        sh '''
          set -eu
          docker push "$VOTE_IMAGE:$IMAGE_TAG"
          docker push "$RESULT_IMAGE:$IMAGE_TAG"
          docker push "$WORKER_IMAGE:$IMAGE_TAG"
        '''
      }
    }

    stage('Deploy') {
      steps {
        withCredentials([
          string(credentialsId: 'ngrok-token', variable: 'NGROK_AUTHTOKEN'),
          file(credentialsId: 'kubeconfig', variable: 'KUBECONFIG')
        ]) {
          sh '''
            set -eu

            kubectl create namespace "$K8S_NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -
            kubectl -n "$K8S_NAMESPACE" create secret generic ngrok-token-secret \
              --from-literal=token="$NGROK_AUTHTOKEN" \
              --dry-run=client -o yaml | kubectl apply -f -

            kubectl apply -f "$K8S_MANIFEST_DIR/"

            kubectl -n "$K8S_NAMESPACE" set image deployment/vote vote="$VOTE_IMAGE:$IMAGE_TAG"
            kubectl -n "$K8S_NAMESPACE" set image deployment/result result="$RESULT_IMAGE:$IMAGE_TAG"
            kubectl -n "$K8S_NAMESPACE" set image deployment/worker worker="$WORKER_IMAGE:$IMAGE_TAG"

            kubectl -n "$K8S_NAMESPACE" rollout status deployment/vote --timeout=90s
            kubectl -n "$K8S_NAMESPACE" rollout status deployment/result --timeout=90s
            kubectl -n "$K8S_NAMESPACE" rollout status deployment/worker --timeout=90s
          '''
        }
      }
    }
  }

  post {
    always {
      sh 'docker logout "$DOCKERHUB_REGISTRY" || true'
    }
  }
}
